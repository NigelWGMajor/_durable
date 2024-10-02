namespace Models
{
    public enum Disruptions
    {
        Crash, // throw in orchestrator 
        Wait,  // throw accessing metadata
        Pass,  // complete activity successfully
        Choke, // delay for resources
        Stall, // fail activity retryably
        Fail,  // fail activity fatally
        Drag,  // take a long time to complete (but not time outr)
        Stick  // take long enough to time out
    }
}
