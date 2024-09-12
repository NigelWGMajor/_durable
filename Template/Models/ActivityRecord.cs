namespace Degreed.SafeTest
{
    public class ActivityRecord
    {
        public string KeyId { get; set; } = "";
        public string ActivityName { get; set; } = "";
        public ActivityState State { get; set; } = ActivityState.unknown;
        public DateTimeOffset Started { get; set; }
        public DateTimeOffset Ended { get; set; }
        public DateTimeOffset Modified { get; set; }
        public string Notes { get; set; } = "";
        public int ProcessId { get; set; }
        public int Instances { get; set; }
    }
}