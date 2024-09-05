namespace Degreed.SafeTest
{

    public class DataStore
    {
        private Dictionary<string, ActivityRecord> _data;
        public DataStore()
        {
            _data = new Dictionary<string, ActivityRecord>();
        }

        public ActivityRecord? Get(string key)
        {
            if (
                _data.ContainsKey(key)
                return _data[key];
            )
            return null;
        }
    }
}