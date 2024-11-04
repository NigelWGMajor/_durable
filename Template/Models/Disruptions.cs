namespace Models
{
    public enum Disruption
    {
        Wait, // throw accessing metadata
        Crash, // throw in orchestrator
        Pass, // complete activity successfully

        // Choke, // delay for resources
        Stall, // fail activity recoverably
        Fail, // fail activity fatally
        Drag, // take a long time to complete (but not time out)
        Stick, // take long enough to time out
        Choke // emulate diminished resources
    }

    public static class DisruptionExtensions
    {
        public static bool Matches(this Disruption disruption, string text)
        {
            return text.ToLower() == disruption.ToString().ToLower();
        }
    }
}
