namespace Degreed.SafeTest
{
    // defines the conventions for dealing with metadata related to the flow management processes.

    // Lifecyle:

    




    public interface IDataStore
    {
        Task<ActivityRecord> GetActivityState(long ReportLogId);
        Task SetActivityState(long ReportLogId, ActivityRecord record);

    }

    public class DataStore
    {
        public DataStore()
        {
           
        }

       
    }
}