namespace Degreed.SafeTest
{
    public class ActivityRecord
    {
        public string ActivityName { get; set; } = "";
        public ActivityState State { get; set; } = ActivityState.unknown;
         
        public DateTimeOffset TimeStamp { get; set; }
    }
}