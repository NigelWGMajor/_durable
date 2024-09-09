namespace Models;

private enum ActivityState
{
    // this needs to match the sequence of definitions in the database
    // This is predefined in the setup sql
    unknown = 0,
    Stuck,
    Deferred,
    Active,
    Completed,
    Stalled,
    Failed
}

public class ActivityStates
{
    /// <summary>
    /// State is not yet established
    /// </summary>
    public const int unknown = ActivityState.unknown;
    /// <summary>
    /// Activity is in an unknown, unresponsive or timed-out state
    /// </summary>
    public const int Stuck = ActivityState.Stuck;
    /// <summary>
    /// Activity is paused because of a resource shortage
    /// </summary>
    public const int Deferred = ActivityState.Deferred;
    /// <summary>
    /// Activity is busy with its work
    /// </summary>
    public const int Active = ActivityState.Active;
    /// <summary>
    /// Activity has completed its work successfully
    /// </summary>
    public const int Completed = ActivityState.Completed;
    /// <summary>
    /// Activity has experienced a non-fatal error and may be retried
    /// </summary>
    public const int Stalled = ActivityState.Stalled;
    /// <summary>
    /// Activity has experiences a fatal error
    /// </summary>
    public const int Failed = ActivityState.Failed;
}
