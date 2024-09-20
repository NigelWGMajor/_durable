using System.Text.Json.Serialization;

namespace Degreed.SafeTest
{
    public class ActivityRecord
    {
        [JsonPropertyName("keyId")]
        public string KeyId { get; set; } = "";

        [JsonPropertyName("activityName")]
        public string ActivityName { get; set; } = "";
        [JsonIgnore]
        public ActivityState State { get; set; } = ActivityState.unknown;

        [JsonPropertyName("activityState")]
        public int ActivityStateCode
        {
            get { return (int)State; }
            set { State = (ActivityState)value; }
        }

        [JsonPropertyName("timeStarted")]
        public DateTime TimeStarted { get; set; }

        [JsonPropertyName("timeEnded")]
        public DateTime TimeEnded { get; set; }

        [JsonPropertyName("timeUpdated")]
        public DateTime TimeUpdated { get; set; }

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = "";

        [JsonPropertyName("processId")]
        public string ProcessId { get; set; } = "";

        [JsonPropertyName("instanceNumber")]
        public int InstanceNumber { get; set; }
        [JsonPropertyName("count")]
        public int Count { get; set; } = 0;
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
