using System;
using System.Data;
using System.Resources;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Degreed.SafeTest;

public interface IDataStore
{
    Task<ActivityRecord> ReadActivityState(long KeyId);
    Task WriteActivityState(ActivityRecord record);
}
public class DataStore : IDataStore
{
    private readonly string _connectionString;

    public DataStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<ActivityRecord> ReadActivityState(long KeyId)
    {
        try
        {
            string json = "";
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string queryName = "ReportFlowState_Read";
                using (SqlCommand command = new SqlCommand(queryName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(
                        new SqlParameter("@KeyId", SqlDbType.BigInt) { Value = KeyId }
                    );

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            json = reader.GetString(0);
                        }
                        else
                        {
                            return new ActivityRecord
                            {
                                Notes = "Not found",
                                State = ActivityState.unknown
                            };
                        }
                    }
                }
            }
            if (String.IsNullOrEmpty(json))
                return new ActivityRecord { Notes = "Not found", State = ActivityState.unknown };
            return JsonSerializer.Deserialize<ActivityRecord>(json);
        }
        catch (Exception ex)
        {
            throw new FlowManagerException("Unable to read current ActivityRecord", ex);
        }
    }

    public async Task WriteActivityState(ActivityRecord record)
    {
        try
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string queryName = "ReportFlowState_Write";
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
