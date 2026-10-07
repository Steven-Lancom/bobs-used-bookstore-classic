using Microsoft.Extensions.Configuration;

namespace BobsBookstoreClassic.Data
{
    public sealed class BookstoreConfiguration
    {
        private static readonly Lazy<BookstoreConfiguration> Lazy = new Lazy<BookstoreConfiguration>(() => new BookstoreConfiguration());

        private static BookstoreConfiguration Instance => Lazy.Value;

        private readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>();

        private BookstoreConfiguration() { }

        public static void Initialize(IConfiguration configuration)
        {
            var appSettingsSection = configuration.GetSection("AppSettings");
            foreach (var setting in appSettingsSection.GetChildren())
            {
                if (setting.Value != null)
                {
                    Instance._appSettings[setting.Key] = setting.Value;
                }
            }

            var connStringsSection = configuration.GetSection("ConnectionStrings");
            foreach (var setting in connStringsSection.GetChildren())
            {
                if (setting.Value != null)
                {
                    Instance._connectionStrings[setting.Key] = setting.Value;
                }
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string? GetSetting(string key)
        {
            return Instance._appSettings.TryGetValue(key, out var value) ? value : null;
        }

        public static T? GetSetting<T>(string key)
        {
            if (!Instance._appSettings.TryGetValue(key, out var value)) return default;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static void AddConnectionString(string key, string value)
        {
            Instance._connectionStrings[key] = value;
        }

        public static string? GetConnectionString(string key)
        {
            return Instance._connectionStrings.TryGetValue(key, out var value) ? value : null;
        }
    }
}
