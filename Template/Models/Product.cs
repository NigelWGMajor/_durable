using Degreed.SafeTest;
public class Product
{
    public required Payload Payload { get; set; }
    public ActivityState LastState { get; set; } = ActivityState.unknown;
    public List<ActivityRecord> ActivityHistory { get; set; } = new List<ActivityRecord>();
}