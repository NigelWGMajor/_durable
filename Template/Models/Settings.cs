namespace Models
{
    public static class Settings
    {
        public static int StallCap { get; set; } = 10;
        public static int StickCap { get; set; } = 2;
        public static int DelayCap { get; set; } = 5;
        public static TimeSpan MaximumActivityTime { get; set; } = TimeSpan.FromHours(6);
        public static TimeSpan DelayTime { get; set; } = TimeSpan.FromMinutes(30);
    }
}