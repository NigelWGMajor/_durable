namespace Degreed.SafeTest;

public enum ActivityState
{
    unknown = 0,
    Ready,       // available to start processing
    Active,      // currently being processed
    Redundant,   // a new orchestration that is colliding with an active activity
    Deferred,    // activity is waiting pending better resources
    Completed,   // activity completed successfully
    Stuck,       // activity has exceeded time limits
    Stalled,     // activity had non-fatal error, expecting retry or orchestrator failure
    Failed,      // unrecoverable error to be passed to the orchestrator
    Successful,  // ultimately complete
    Unsuccessful // ultimately failed
    // The entire operation i.e. chain of activities is ultimately Successful or Unsuccessful
}
