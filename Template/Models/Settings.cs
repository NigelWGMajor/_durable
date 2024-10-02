namespace Models
{
    public static class Settings
    {
        /// <summary>
        /// How many times to retry when an activity times out
        /// </summary>
        /// <value></value>
        public static int StickCap { get; set; } = 2;
        /// <summary>
        /// How often to delay an activity because resources are impacted
        /// </summary>
        /// <value></value>
        public static int ChokeCap { get; set; } = 5;
        public static TimeSpan MaximumActivityTime { get; set; } = TimeSpan.FromHours(6);
        public static TimeSpan ChokeTime { get; set; } = TimeSpan.FromMinutes(30);
    }
}