using System.Text.Json.Serialization;

namespace Models
{
    public class ActivitySettings
    {
        [JsonPropertyName("ActivityName")]
        public string ActivityName { get; set; } = "Default";
        [JsonPropertyName("ActivityTimeout")]
        public double? ActivityTimeout { get; set; } // = TimeSpan.FromHours(1);
        // increases the number of retries to 10
        [JsonPropertyName("LoadFactor")]
        public double LoadFactor { get; set; } // = false;
        // increases the InitialDelay to 0.2
        [JsonPropertyName("MaximumDelayCount")]
        public int MaximumDelayCount { get; set; } // = false;
        [JsonPropertyName("PartitionId")]
        public int? PartitionId { get; set; } // = 0;
           
    }
}