using Degreed.SafeTest;

public class Product<T>
{
    public T? Value { get; set; }
    public ActivityState LastState { get; set; } = ActivityState.unknown;
    public List<ActivityRecord> ActivityHistory { get; set; } = new List<ActivityRecord>();
}