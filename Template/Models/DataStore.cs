using System;
using System.Data;
using System.Diagnostics;
using System.Resources;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Models;

namespace Degreed.SafeTest;

public interface IDataStore
{
    Task<ActivityRecord> ReadActivityStateAsync(string KeyId);
    Task WriteActivityStateAsync(ActivityRecord record);
}

[DebuggerStepThrough]
public class DataStore : IDataStore
{
    private readonly string _connectionString;
    private const string _read_activity_ = "rpt.OperationFlowState_Read";
    private const string _read_settings_ = "rpt.ActivitySettings_Read";
    private const string _write_activity_ = "rpt.OperationFlowState_Write";
    private const string _write_settings_ = "rpt.ActivitySettings_Write";
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public DataStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<ActivityRecord> ReadActivityStateAsync(string keyId)
    {
        await _semaphore.WaitAsync();
        try
        {
            return ReadActivityStateAsyncInternal(keyId).GetAwaiter().GetResult();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<ActivityRecord> ReadActivityStateAsyncInternal(string keyId)
    {
        try
        {
            string? json = "";
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string queryName = _read_activity_;
                using (SqlCommand command = new SqlCommand(queryName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(
                        new SqlParameter("@UniqueKey", SqlDbType.NVarChar, 100) { Value = keyId }
                    );

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            json = reader.IsDBNull(0) ? null : reader.GetString(0);
                        }
                    }
                }
            }
            if (String.IsNullOrEmpty(json))
                return new ActivityRecord
                {
                    UniqueKey = keyId,
                    State = ActivityState.unknown
                };
            return JsonSerializer.Deserialize<ActivityRecord>(json) 
               ?? new ActivityRecord();
        }
        catch (Exception ex)
        {
            throw new FlowManagerRecoverableException("Metadata store: Unable to read current ActivityRecord", ex);
        }
    }

    public async Task WriteActivityStateAsync(ActivityRecord record)
    {
        await _semaphore.WaitAsync();
        try
        {
            WriteActivityStateInternalAsync(record).GetAwaiter().GetResult();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task WriteActivityStateInternalAsync(ActivityRecord record)
    {
        try
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string queryName = _write_activity_;
                string json = JsonSerializer.Serialize(record);

                using (SqlCommand command = new SqlCommand(queryName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(
                        new SqlParameter("@json", SqlDbType.NVarChar) { Value = json }
                    );
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            throw new FlowManagerRecoverableException("Metadata store: Unable to write current ActivityRecord", ex);
        }
    }

    public async Task WriteActivitySettings(ActivitySettings settings)
    {
        await _semaphore.WaitAsync();
        try
        {
            WriteActivitySettingsInternalAsync(settings).GetAwaiter().GetResult();
        }
        finally
        {
            _semaphore.Release();
        }
    }
    private async Task WriteActivitySettingsInternalAsync(ActivitySettings settings)
    {
        try
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string queryName = _write_settings_;
                string json = JsonSerializer.Serialize(settings);

                using (SqlCommand command = new SqlCommand(queryName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(
                        new SqlParameter("@json", SqlDbType.NVarChar) { Value = json }
                    );
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            throw new FlowManagerRecoverableException("Metadata store: Unable to write current ActivitySettings", ex);
        }
    }
    public async Task<ActivitySettings> ReadActivitySettings(string activityName)
    {
        await _semaphore.WaitAsync();
        try
        {
            return ReadActivitySettingsInternalAsync(activityName).GetAwaiter().GetResult();
        }
        finally
        {
            _semaphore.Release();
        }
    }
    private async Task<ActivitySettings> ReadActivitySettingsInternalAsync(string activityName)
    {
        try
        {
            string? json = "";
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string queryName = _read_settings_;
                using (SqlCommand command = new SqlCommand(queryName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(
                        new SqlParameter("@ActivityName", SqlDbType.NVarChar, 100) { Value = activityName }
                    );

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            json = reader.IsDBNull(0) ? null : reader.GetString(0);
                        }
                    }
                }
            }
            if (String.IsNullOrEmpty(json))
                return new ActivitySettings
                {
                    ActivityName = activityName
                };
            return JsonSerializer.Deserialize<ActivitySettings>(json) 
               ?? new ActivitySettings();
        }
        catch (Exception ex)
        {
            throw new FlowManagerRecoverableException("Metadata store: Unable to read current ActivitySettings", ex);
        }
    }
}
