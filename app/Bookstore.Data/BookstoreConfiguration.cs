using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace BobsBookstoreClassic.Data
{
    /// <summary>
    /// Application configuration wrapper. Initialized from IConfiguration on startup,
    /// then allows runtime overrides (e.g. values fetched from AWS SSM Parameter Store).
    /// </summary>
    public sealed class BookstoreConfiguration
    {
        private static readonly Lazy<BookstoreConfiguration> Lazy =
            new Lazy<BookstoreConfiguration>(() => new BookstoreConfiguration());

        private static BookstoreConfiguration Instance => Lazy.Value;

        private readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private BookstoreConfiguration() { }

        /// <summary>
        /// Initialize from ASP.NET Core IConfiguration. Call this once at startup.
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            // Load all AppSettings section keys
            var appSettingsSection = configuration.GetSection("AppSettings");
            foreach (var child in appSettingsSection.GetChildren())
            {
                Instance._appSettings[child.Key] = child.Value ?? string.Empty;

                // Environment variable override (key with slashes replaced by double underscores)
                var envKey = child.Key.Replace("/", "__");
                var envValue = Environment.GetEnvironmentVariable(envKey);
                if (envValue != null)
                {
                    Instance._appSettings[child.Key] = envValue;
                }
            }

            // Also allow direct environment variable overrides using the original key
            foreach (var key in Instance._appSettings.Keys)
            {
                var envValue = Environment.GetEnvironmentVariable(key);
                if (envValue != null)
                {
                    Instance._appSettings[key] = envValue;
                }
            }

            // Load connection strings
            var connectionStringsSection = configuration.GetSection("ConnectionStrings");
            foreach (var child in connectionStringsSection.GetChildren())
            {
                Instance._connectionStrings[child.Key] = child.Value ?? string.Empty;
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            if (Instance._appSettings.TryGetValue(key, out var value))
                return value;
            return string.Empty;
        }

        public static T GetSetting<T>(string key)
        {
            var value = GetSetting(key);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static void AddConnectionString(string key, string value)
        {
            Instance._connectionStrings[key] = value;
        }

        public static string GetConnectionString(string key)
        {
            if (Instance._connectionStrings.TryGetValue(key, out var value))
                return value;
            return string.Empty;
        }
    }
}
