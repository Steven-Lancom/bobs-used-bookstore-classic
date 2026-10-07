using BobsBookstoreClassic.Data;
using Bookstore.Common;
using NLog;
using NLog.AWS.Logger;
using NLog.Config;
using NLog.Targets;

namespace Bookstore.Web
{
    public static class LoggingSetup
    {
        public static void ConfigureLogging(BookstoreConfiguration config)
        {
            var nlogConfig = new LoggingConfiguration();

            Target loggingTarget;

            if (config.GetSetting("Services/LoggingService") == "aws")
            {
                loggingTarget = new AWSTarget { LogGroup = Constants.AppName };
            }
            else
            {
                loggingTarget = new DebuggerTarget();
            }

            nlogConfig.AddTarget("aws", loggingTarget);

            nlogConfig.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, loggingTarget));

            LogManager.Configuration = nlogConfig;
        }
    }
}
