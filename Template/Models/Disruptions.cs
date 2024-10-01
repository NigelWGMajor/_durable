namespace Models
{
    public enum Disruptions
    {
        crash, // throw in orchestrator 
        wait,  // throw accessing metadata
        pass,  // complete activity successfully
        choke, // delay for resources
        stall, // fail activity retryably
        fail,  // fail activity fatally
        drag,  // take a long time to complete (but not time outr)
        stick  // take long enough to time out
    }
}
