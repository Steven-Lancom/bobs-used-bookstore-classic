using Microsoft.Extensions.Configuration;

namespace BobsBookstoreClassic.Data
{
    public sealed class BookstoreConfiguration
    {
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, string> _overrides = new Dictionary<string, string>();

        public BookstoreConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void AddSetting(string key, string value)
        {
            _overrides[key] = value;
        }

        public string GetSetting(string key)
        {
            // Environment variable takes precedence, then overrides, then configuration
            var envValue = Environment.GetEnvironmentVariable(key);
            if (envValue != null) return envValue;

            if (_overrides.TryGetValue(key, out var overrideValue)) return overrideValue;

            // Support both slash-separated keys (Files/BucketName) and colon-separated (Files:BucketName)
            return _configuration[key.Replace("/", ":")] ?? _configuration[key];
        }

        public T GetSetting<T>(string key)
        {
            var value = GetSetting(key);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public void AddConnectionString(string key, string value)
        {
            _overrides[$"ConnectionStrings:{key}"] = value;
        }

        public string GetConnectionString(string key)
        {
            if (_overrides.TryGetValue($"ConnectionStrings:{key}", out var overrideValue)) return overrideValue;

            return _configuration.GetConnectionString(key);
        }
    }
}
