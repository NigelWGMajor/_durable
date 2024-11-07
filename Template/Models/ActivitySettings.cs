using System.Text.Json.Serialization;

namespace Models
{
    public class ActivitySettings
    {
        [JsonPropertyName("ActivityName")]
        public string ActivityName { get; set; } = "Default";
        [JsonPropertyName("NumberOfRetries")]
        public int? NumberOfRetries { get; set; } // = 8;
        [JsonPropertyName("InitialDelay")]
        public double? InitialDelay { get; set; } // = TimeSpan.FromHours(0.1);
        [JsonPropertyName("BackOffCoefficient")]
        public double? BackOffCoefficient { get; set; } // = 1.414214;
        [JsonPropertyName("MaximumDelay")]
        public double? MaximumDelay { get; set; } // = TimeSpan.FromHours(2.5);
        [JsonPropertyName("RetryTimeout")]
        public double? RetryTimeout { get; set; } // = TimeSpan.FromHours(24);
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