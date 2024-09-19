namespace Degreed.SafeTest
{
    public class ActivityRecord
    {
        public string KeyId { get; set; } = "";
        public string ActivityName { get; set; } = "";
        public ActivityState State { get; set; } = ActivityState.unknown;
        public DateTime TimeStarted { get; set; }
        public DateTime TimeEnded { get; set; }
        public DateTime TimeUpdated { get; set; }
        public string Notes { get; set; } = "";
        public string ProcessId { get; set; } = "";
        public int InstanceNumber { get; set; }
    }
    public static class ActivityRecordExtender
    {
        public static void MarkStartTime(this ActivityRecord record)
        {
            record.TimeStarted = DateTime.UtcNow;
        }
        public static void MarkEndTime(this ActivityRecord record)
        {
            record.TimeEnded = DateTime.UtcNow;
        }
        /// <summary>
        /// Update the product LastSate and history using this ActivityRecord.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="product"></param>
        public static void UpdateProductState(this ActivityRecord record, Product product)
        {
            product.ActivityHistory.Add(record);
            product.LastState = record.State;
        }
    }
}