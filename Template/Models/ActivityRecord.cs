namespace Degreed.SafeTest
{
    public class ActivityRecord
    {
        public string KeyId { get; set; } = "";
        public string ActivityName { get; set; } = "";
        public ActivityState State { get; set; } = ActivityState.unknown;
        public DateTimeOffset TimeStarted { get; set; }
        public DateTimeOffset TimeEnded { get; set; }
        public DateTimeOffset TimeUpdated { get; set; }
        public string Notes { get; set; } = "";
        public string ProcessId { get; set; } = "";
        public int InstanceNumber { get; set; }
    }
}