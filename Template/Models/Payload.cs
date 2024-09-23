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
    /// <summary>
    /// The TestScenarios string allows injection of test scenarios to be processed sequentially by the Flow Manager.
    /// Allowable terms are:
    /// *           Alias for Pass 
    /// Pass        Complete successfully
    /// Fail        Fail fatally
    /// Defer       Delay execution to allow resources to replenish
    /// Stall       Fail retryably
    /// Stick       Fail to return within reasonbable time
    /// Block       Execute slowly to allow reentrancy
    /// CapStall    Stall too many times
    /// CapStick    Stick too many times
    /// CapDefer    Defer too many times
    /// (comment)   Comments are allowed in parentheses 
    /// Scenarios are applied to the same activity until it either passes or fails. Further scenarios will apply to the next activity.
    /// When no more scenario are supplied, * is assumed. the string is space-delimited, redundant white space is ignored. 
    /// </summary>
    /// <value></value>
    public string TestScenarios { get; set; } = "";
    // insert whatever your domain needs!
}
