// this class contains the domain-specific content that is passed between operations.
// The flow history is attached to the enclosing Product.
// This must be serializable.
using Degreed.SafeTest;
using System.Diagnostics;

[DebuggerStepThrough]
public class Payload
{
    public string Name { get; set; } = "";
    public string InstanceId { get; set; } = "";
    public int Id { get; set; }
    public ActivityState[] TestStates { get; set; } = Array.Empty<ActivityState>();
    // insert whatever your domain needs!
}
