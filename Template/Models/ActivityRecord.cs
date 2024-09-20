using System.Text.Json.Serialization;
using System.Diagnostics;

namespace Degreed.SafeTest
{
    [DebuggerStepThrough]
    public class ActivityRecord
    {
        [JsonPropertyName("KeyId")]
        public string KeyId { get; set; } = "";

        [JsonPropertyName("ActivityName")]
        public string ActivityName { get; set; } = "";

        [JsonIgnore]
        public ActivityState State { get; set; } = ActivityState.unknown;

        [JsonPropertyName("ActivityState")]
        public int ActivityStateCode
        {
            get { return (int)State; }
            set { State = (ActivityState)value; }
        }

        [JsonPropertyName("TimeStarted")]
        public DateTime TimeStarted { get; set; }

        [JsonPropertyName("TimeEnded")]
        public DateTime TimeEnded { get; set; }

        [JsonPropertyName("TimeUpdated")]
        public DateTime TimeUpdated { get; set; }

        [JsonPropertyName("Notes")]
        public string Notes { get; set; } = "";

        [JsonPropertyName("ProcessId")]
        public string ProcessId { get; set; } = "";

        [JsonPropertyName("InstanceNumber")]
        public int InstanceNumber { get; set; }

        [JsonPropertyName("Count")]
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
