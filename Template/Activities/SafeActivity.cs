using Degreed.SafeTest;
using Microsoft.Extensions.Configuration;

public class SafeActivity
{
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
                throw new FlowManagerRecoverableException("MetadataStore connection not provided");
        }
    }

    public async Task<Product> ProcessAsync()
    {
        // quick kill if redundant
        if (_product.IsRedundant)
            return _product;
        try
        {
            while (_current.State != ActivityState.Active)
            {
                await PreProcess();
                if (_product.IsRedundant)
                    return _product;
                if (_current.State == ActivityState.Active)
                {
                    await Process();
                }
            }
            await _store.WriteActivityStateAsync(_current);
            return _product;
        }
        catch (FlowManagerRecoverableException ex)
        {
            _current.RetryCount++;
            _current.Reason = ex.Message;
            _current.AddTrace($"Recoverable error encountered!");
            throw;
        }
        catch (FlowManagerFatalException ex)
        {
            _current.RetryCount++;
            _current.Reason = ex.Message;
            _current.AddTrace($"Fatal error encountered!");
            throw;
        }
        catch (FlowManagerInfraException ex)
        {
            _current.RetryCount++;
            _current.Reason = ex.Message;
            _current.AddTrace($"Infrastructure error encountered!");
            throw;
        }
        catch (Exception ex)
        {
            _current.RetryCount++;
            _current.Reason = ex.Message;
            _current.AddTrace($"Unknown exception encountered!");
            throw new FlowManagerRecoverableException($"Unknown: {ex.Message}");
        }
    }

    private async Task PreProcess()
    {
        _current = await _store.ReadActivityStateAsync(_product.Payload.UniqueKey);

        switch (_current.State)
        {
            case ActivityState.unknown:
                _current.ActivityName = _product.ActivityName;
                _current.OperationName = _product.OperationName;
                _current.SequenceNumber = 0;
                _current.State = ActivityState.Ready;
                _current.Disruptions = _product.Disruptions;
                _current.AddTrace(
                    $"New {_current.OperationName} {string.Join('|', _current.Disruptions)}."
                );
                break;
            case ActivityState.Deferred:
            case ActivityState.Stalled:
                _current.SequenceNumber++;
                _current.AddTrace($"Was {_current.State} now ready (available to process).");
                _current.State = ActivityState.Ready;
                break;
            case ActivityState.Ready:
                _current.SequenceNumber++;
                _current.AddTrace($"Was {_current.State} now active (being processed).");
                _current.State = ActivityState.Active;
                break;
            case ActivityState.Completed:
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
        // set up parallel timer
        var settings = await _store.ReadActivitySettingsAsync(_current.ActivityName);
        var timeout = settings.ActivityTimeout.GetValueOrDefault(1.0);
        try
        {
            var executionTask = Task.Run(() => _executable(_product));
            var timeoutTask = Task.Delay(TimeSpan.FromHours(timeout));
            var effectiveTask = await Task.WhenAny(executionTask, timeoutTask);
            if (effectiveTask == timeoutTask)
            {
                _current.State = ActivityState.Stalled;
                _current.AddTrace($"Activity timed out after {timeout} hours.");
                _current.Reason = "Activity timed out.";
                throw new FlowManagerRecoverableException("Activity timed out.");
            }
            _product = await executionTask;
            _current.State = ActivityState.Completed;
        }
        catch (FlowManagerRecoverableException ex)
        {
            _current.State = ActivityState.Stalled;
            _current.RetryCount++;
            _current.Reason = ex.Message;
            _current.AddTrace($"Recoverable error encountered!");
            throw;
        }
        catch (FlowManagerFatalException ex)
        {
            _current.State = ActivityState.Failed;
            _current.RetryCount++;
            _current.Reason = ex.Message;
            _current.AddTrace($"Fatal error encountered!");
            throw;
        }
        catch (FlowManagerInfraException ex)
        {
            _current.State = ActivityState.Deferred;
            _current.RetryCount++;
            _current.Reason = ex.Message;
            _current.AddTrace($"Infrastructure error encountered!");
            throw;
        }
        catch (Exception ex)
        {
            _current.State = ActivityState.Stalled;
            _current.RetryCount++;
            _current.Reason = ex.Message;
            _current.AddTrace($"Unknown exception encountered!");
            throw new FlowManagerRecoverableException($"Unknown: {ex.Message}");
        }
        finally
        {
            _current.TimeEnded = DateTime.UtcNow;
            await _store.WriteActivityStateAsync(_current);
        }
        return;
    }
}
