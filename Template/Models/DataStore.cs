using System;
using System.Data;
using System.Resources;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Degreed.SafeTest;

public interface IDataStore
{
    Task<ActivityRecord> ReadActivityStateAsync(string KeyId);
    Task WriteActivityStateAsync(ActivityRecord record);
}
public class DataStore : IDataStore
{
    private readonly string _connectionString;
    private const string _read_activity_ = "rpt.ReportFlowState_Read";
    private const string _write_activity_ = "rpt.ReportFlowState_Write";
    public DataStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<ActivityRecord> ReadActivityStateAsync(string keyId)
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
                        new SqlParameter("@KeyId", SqlDbType.NVarChar) { Value = keyId }
                    );

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            json = 
                                reader.IsDBNull(0) ?
                                null :
                                reader.GetString(0);
                        }
                    }
                }
            }
            if (String.IsNullOrEmpty(json))
                return new ActivityRecord { KeyId = keyId, Notes = "Not found", State = ActivityState.unknown };
            return JsonSerializer.Deserialize<ActivityRecord>(json);
        }
        catch (Exception ex)
        {
            throw new FlowManagerException("Unable to read current ActivityRecord", ex);
        }
    }

    public async Task WriteActivityStateAsync(ActivityRecord record)
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
            throw new FlowManagerException("Unable to write current ActivityRecord", ex);
        }
    }
}