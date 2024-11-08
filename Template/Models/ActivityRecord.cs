using System.Text.Json.Serialization;
using System.Diagnostics;
using Models;

namespace Degreed.SafeTest
{
    [DebuggerStepThrough]
    public class ActivityRecord
    {
        [JsonPropertyName("UniqueKey")]
        public string UniqueKey { get; set; } = "";

        [JsonPropertyName("OperationName")]
        public string OperationName { get; set; } = "";

        [JsonPropertyName("ActivityName")]
        public string ActivityName { get; set; } = "";

        [JsonPropertyName("ActivityStateName")]
        public string ActivityStateName
        {
            get => $"{State}";
            set { State = (ActivityState)System.Enum.Parse(typeof(ActivityState), value); }
        }

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

        [JsonPropertyName("Trace")]
        public string Trace { get; set; } = "";

        [JsonPropertyName("InstanceId")]
        public string InstanceId { get; set; } = "";

        [JsonPropertyName("SequenceNumber")]
        public int SequenceNumber { get; set; }

        [JsonPropertyName("RetryCount")]
        public int RetryCount { get; set; } = 0;

        [JsonPropertyName("Reason")]
        public string Reason { get; set; } = "";

        [JsonPropertyName("Disruptions")]
        public string Disruptions
        {
            get => string.Join("|", DisruptionArray);
            set => DisruptionArray = value.Split('|');
        }

        [JsonIgnore]
        public string[] DisruptionArray { get; set; } = new string[] { };

        [JsonPropertyName("HostServer")]
        public string HostServer { get; set; } = "";
    }

    [DebuggerStepThrough]
    public static class ActivityRecordExtender
    {
        private static readonly string _eol_ = "|";

        public static void MarkStartTime(this ActivityRecord record)
        {
            record.TimeStarted = DateTime.UtcNow;
        }

        public static void MarkEndTime(this ActivityRecord record)
        {
            record.TimeEnded = DateTime.UtcNow;
        }

        public static void AddTrace(this ActivityRecord record, string message)
        {
            record.Reason = message;
            record.Trace =
                $"{record.Trace}{_eol_}[{record.SequenceNumber}]:{message}({record.TimeEnded - record.TimeStarted})";
        }

        public static void PopDisruption(this ActivityRecord record)
        {
            if (record.DisruptionArray.Length == 0)
                return;
            else
            {
                string[] temp = new string[record.DisruptionArray.Length - 1];
                for (int i = 0; i < temp.Length; i++)
                {
                    temp[i] = record.DisruptionArray[i + 1];
                }
                record.DisruptionArray = temp;
                return;
            }
        }

        public static bool NextDisruptionIs(this ActivityRecord record, Disruption disruption)
        {
            if (record.DisruptionArray.Length == 0)
                return false;
            else
            {
                return disruption.Matches(record.DisruptionArray[0]);
            }
        }

        /// <summary>
        /// Update the product LastSate and history using this ActivityRecord.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="product"></param>
        public static void TimestampRecord_UpdateProductStateHistory(
            this ActivityRecord record,
            Product product
        )
        {
            record.MarkEndTime();
            record.SequenceNumber++;
            product.ActivityHistory.Add(record);
            product.LastState = record.State;
        }
        /* refactor: extend ActivityRecord to wrap the Metadata access */
    }
}
