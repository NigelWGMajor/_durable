using Microsoft.Identity.Client;

namespace Models
{
    public static class Settings
    {
        /// <summary>
        /// How many times to retry when activity times out 
        /// </summary>
        public static int StickCap { get; set; } = 2;
        /// <summary>
        /// The longest time an activity os allowed to run
        /// </summary>
        public static TimeSpan MaximumActivityTime { get; set; } = TimeSpan.FromHours(12);
        /// <summary>
        /// How long to wait if metadata is off line
        /// </summary>
        public static TimeSpan WaitTime { get; set; } = TimeSpan.FromMinutes(2);
        
    }
}