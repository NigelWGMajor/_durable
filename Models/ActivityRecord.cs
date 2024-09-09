namespace Models;

public class ActivityRecord
{
    public string ActivityName { get; set; }
    public ActivityState State { get; set; }
     
    public DateTimeOffset TimeStamp { get; set; } 
    public int Sequence { get; set; }
    public string Message { get; set; }
}