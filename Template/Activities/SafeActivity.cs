using Degreed.SafeTest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

public class SafeActivity
{
    //! temp
    private Product _product;
    private Func<Product, Task<Product>> _executable;
    private static DataStore _store = new DataStore("");
    private ActivityRecord _current = new();

    public SafeActivity(Func<Product, Task<Product>> executable, Product product)
    {
        _product = product;
        _executable = executable;

        if (_store.IsValid == false)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();
            string? temp = configuration["Values:MetadataStore"];
            if (temp != null)
                _store = new DataStore(temp);
            else
                throw new FlowManagerInfraException("MetadataStore connection not provided");
        }
    }

    public async Task<Product> ProcessAsync()
    {
        // quick kill if redundant
        if (_product.IsRedundant || _product.LastState == ActivityState.Failed)
            return _product;
        try
        {
            do
            {
                await PreProcess();
                if (_product.IsRedundant)
                    return _product;
                if (_current.State == ActivityState.Active)
                {
                    _current.SequenceNumber++;
                    await Process();
                    return _product; // happy path!
                }
            } while (_current.State != ActivityState.Active);
            return _product;
        }
        catch (FlowManagerRecoverableException ex)
        {
            _current.RetryCount++;
            _current.AddTrace($"Recoverable error encountered! {ex.Message}");
            throw;
        }
        catch (FlowManagerFatalException ex)
        {
            _product.LastState = ActivityState.Failed; 
            return _product;
        }
        catch (FlowManagerInfraException ex)
        {
            _current.RetryCount++;
            _current.AddTrace($"Infrastructure error encountered! {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            _current.RetryCount++;
            _current.AddTrace($"Unknown exception encountered! {ex.Message}");
            throw new FlowManagerRecoverableException($"Unknown: {ex.Message}");
        }
    }

    private async Task PreProcess()
    {
        if (_product.IsRedundant)
            return;
        _current = await _store.ReadActivityStateAsync(_product.Payload.UniqueKey);
        if (
            !string.IsNullOrEmpty(_current.InstanceId) && _product.InstanceId != _current.InstanceId
        )
        {
            _current.SequenceNumber++;
            _current.AddTrace($"Re-entrant call on {_current.State} rejected.");
            _product.IsRedundant = true;
            await _store.WriteActivityStateAsync(_current);
            return;
        }
        // wait disruption emulation
        if (_current.NextDisruptionIs(Models.Disruption.Wait))
        {
            _current.PopDisruption();
            _current.State = ActivityState.Deferred;
            _current.AddTrace("Activity deferred (emulated).");
            await _store.WriteActivityStateAsync(_current);
            throw new FlowManagerInfraException("Activity deferred (emulated).");
        }
        // fail disruption emulation
        if (_current.NextDisruptionIs(Models.Disruption.Fail))
        {
            _current.PopDisruption();
            _current.State = ActivityState.Failed;
            _current.AddTrace("Activity Failed (emulated).");
            await _store.WriteActivityStateAsync(_current);
            throw new FlowManagerFatalException("Activity Failed (emulated).");
        }
        switch (_current.State)
        {
            case ActivityState.unknown:
                _current.ActivityName = _product.ActivityName;
                _current.OperationName = _product.OperationName;
                _current.SequenceNumber = 0;
                _current.State = ActivityState.Ready;
                _current.DisruptionArray = (string[])_product.Disruptions.Clone();
                if (string.IsNullOrEmpty(_current.InstanceId))
                    _current.InstanceId = _product.InstanceId;
                _current.AddTrace($"New {_current.OperationName} {_current.Disruptions}.");
                break;
            case ActivityState.Deferred:
            case ActivityState.Stalled:
                _current.SequenceNumber++;
                _current.AddTrace($"Was {_current.State} now ready (available to process).");
                _current.State = ActivityState.Ready;
                break;
            case ActivityState.Completed:
                _current.SequenceNumber++;
                _current.ActivityName = _product.ActivityName;
                _current.AddTrace($"Was {_current.State} now ready (available to process).");
                _current.State = ActivityState.Ready;
                break;
            case ActivityState.Ready:
                _current.SequenceNumber++;
                _current.AddTrace($"Was {_current.State} now active (being processed).");
                _current.State = ActivityState.Active;
                break;
            case ActivityState.Failed:
            case ActivityState.Successful:
            case ActivityState.Unsuccessful:
                _current.SequenceNumber++;
                _current.AddTrace($"Re-entrant call on {_current.State} rejected.");
                _product.IsRedundant = true;
                break;
            default:
                _current.AddTrace($"Unexpected state in PreProcessor: {_current.State}.");
                break;
        }
        _current.TimeEnded = DateTime.UtcNow;
        await _store.WriteActivityStateAsync(_current);
    }

    private async Task Process()
    {
        if (_product.IsRedundant)
            return;
        // set up parallel timer
        var settings = await _store.ReadActivitySettingsAsync(_current.ActivityName);
        var timeout = settings.ActivityTimeout.GetValueOrDefault(1.0);
        if (_current.NextDisruptionIs(Models.Disruption.Pass))
        {
            _current.PopDisruption();
            _current.State = ActivityState.Completed;
            _current.AddTrace($"Activity passed (emulated).");    
            await _store.WriteActivityStateAsync(_current);
            return;
        }
        try
        {
            bool isTimeoutFaked = false;
            if (_current.NextDisruptionIs(Models.Disruption.Stall))
            {
                _current.PopDisruption();
                _current.State = ActivityState.Stalled;
                await _store.WriteActivityStateAsync(_current);
                throw new FlowManagerRecoverableException("Activity stalled (emulated).");
            }
            else if (_current.NextDisruptionIs(Models.Disruption.Drag)) 
            {
                _current.PopDisruption();
                var t = timeout / 2;
                await Task.Delay(TimeSpan.FromHours(t));
            }
            else if (_current.NextDisruptionIs(Models.Disruption.Stick)) 
            {
                _current.PopDisruption();
                timeout = 1 / 60 / 60 / 100; // 1/100th of a second
                isTimeoutFaked = true;
            }
            var executionTask = Task.Run(() => _executable(_product));
            var timeoutTask = Task.Delay(TimeSpan.FromHours(timeout));
            var effectiveTask = await Task.WhenAny(executionTask, timeoutTask);
            if (effectiveTask == timeoutTask)
            {
                _current.State = ActivityState.Stalled;
                _current.AddTrace($"Activity timed out after {timeout} hours {(isTimeoutFaked ? "(emulated)" : "")}.");
                throw new FlowManagerRecoverableException("Activity timed out.");
            }
            _product = await executionTask;
            _current.AddTrace($"Activity completed successfully.");
            _current.MarkEndTime();
            _current.State = ActivityState.Completed;
        }
        catch (FlowManagerRecoverableException ex)
        {
            _current.State = ActivityState.Stalled;
            _current.RetryCount++;
            _current.AddTrace($"Recoverable error encountered! {ex.Message}");
            throw;
        }
        catch (FlowManagerFatalException ex)
        {
            _current.State = ActivityState.Failed;
            _current.RetryCount++;
            _current.AddTrace($"Fatal error encountered! {ex.Message}");
            throw;
        }
        catch (FlowManagerInfraException ex)
        {
            _current.State = ActivityState.Deferred;
            _current.RetryCount++;
            _current.AddTrace($"Infrastructure error encountered! {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            _current.State = ActivityState.Stalled;
            _current.RetryCount++;
            _current.AddTrace($"Unknown exception encountered!");
            throw new FlowManagerRecoverableException($"Unknown: {ex.Message} {ex.Message}");
        }
        finally
        {
            _current.TimeEnded = DateTime.UtcNow;
            await _store.WriteActivityStateAsync(_current);
        }
        return;
    }
}
