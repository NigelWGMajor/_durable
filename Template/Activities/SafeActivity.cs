using DurableTask.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Worker.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Models;

public class SafeActivity
{
    //! temp
    private Product _product;
    private Func<Product, Task<Product>> _executable;
    private static DataStore _store = new DataStore("");
    private ActivityRecord _current = new();
    private bool CapacityIsCompromised()
    {
        // check the existing load on the system, and the load rating for this activity.
        // If the repeat count is not too high given this, then return false to allow the action to proceed immediately. 
        /* THIS IS PENDING THE DATA MODIFICATIONS TO THE ACTIVITY SETTINGS to include the load factor. */
        /* Also needs a stored procedure to get the current activity load  */
        /* We should make this abstract by having the total load in the 0 - 1.0 range */

        return false;
    }

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
            _product.Errors += $"Fatal error: {ex.Message}";
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
        // If the system is overloaded, we can defer the activity.
        _current = await _store.ReadActivityStateAsync(_product.UniqueKey);
        if (CapacityIsCompromised())
        {
            _current.RetryCount++;
            _current.State = ActivityState.Deferred;
            _current.AddTrace($"Activity {_current.ActivityName} deferred for system load).");
            await _store.WriteActivityStateAsync(_current);
            throw new FlowManagerInfraException("Activity deferred.");
        }
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
        if (_current.TimeStarted == DateTime.MinValue)
        {
            _current.TimeStarted = DateTime.UtcNow;
        }
        // wait disruption emulation is similar to choke, except that choke should be used for
        // delaying when busy, wait implies a system or infrastructure issue.
        if (_current.NextDisruptionIs(Models.Disruption.Wait))
        {
            _current.PopDisruption();
            _current.State = ActivityState.Deferred;
            _current.AddTrace("Activity deferred (emulated).");
            await _store.WriteActivityStateAsync(_current);
            throw new FlowManagerInfraException("Activity deferred (emulated).");
        }
        if (_current.NextDisruptionIs(Models.Disruption.Choke))
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
                _current.OperationName = _product.Name;
                _current.HostServer = _product.HostServer;
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
        {
            return;
        }
        else if (_current.NextDisruptionIs(Models.Disruption.Pass))
        {
            _current.PopDisruption();
            _current.State = ActivityState.Completed;
            _current.AddTrace($"Activity passed (emulated).");
            await _store.WriteActivityStateAsync(_current);
            return;
        }
        try
        {
            CancellationTokenSource cts = new();
            double? limit = (
                await _store.ReadActivitySettingsAsync(_current.ActivityName)
            ).ActivityTimeout;
            if (_current.NextDisruptionIs(Models.Disruption.None))
            {
                _current.PopDisruption();
            }
            else if (_current.NextDisruptionIs(Models.Disruption.Stall))
            {
                _current.PopDisruption();
                _current.State = ActivityState.Ready;
                throw new FlowManagerRecoverableException(
                    $"Activity {_current.ActivityName} stalled (emulated)."
                );
            }
            else if (_current.NextDisruptionIs(Models.Disruption.Stick))
            {
                _current.PopDisruption();
                limit = 0.0001;
            }
            else if (_current.NextDisruptionIs(Models.Disruption.Drag))
            {
                _current.PopDisruption();
                limit = limit / 2.0;
            }
            Task<Product> task_product = _executable(_product);
            Task timeout = Task.Delay(
                TimeSpan.FromHours(limit.GetValueOrDefault(defaultValue: 1.0)),
                cts.Token
            );
            var x = await Task.WhenAny(task_product, timeout);
            if (x == timeout)
            {
                _current.State = ActivityState.Stuck;
                _current.AddTrace($"Activity {_current.ActivityName} timed out.");
                await _store.WriteActivityStateAsync(_current);
                _current.SequenceNumber++;
                throw new FlowManagerRecoverableException(
                    $"Activity {_current.ActivityName} timed out."
                );
            }
            cts.Cancel();
            _product = await (x as Task<Product> ?? Task.FromResult(_product));

            _current.State = _product.LastState;
            _current.AddTrace($"Activity completed successfully.");
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
