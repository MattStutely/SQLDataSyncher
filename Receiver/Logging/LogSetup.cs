using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Filter;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Logger.Config;
using System.Configuration;
using System.Data;
using System.IO;

namespace Logger
{
    public static class LogSetup
    {
        private static string _applicationName;
        public static void Initialize()
        {
            //-- Create a handle to the base Log4Net repository
            var root = ((Hierarchy)LogManager.GetRepository()).Root;

            //-- Load the web.config section
            var settings = LogConfigurationSection.Settings;

            //-- Get the app name for the loggers
            if (string.IsNullOrEmpty(settings.ApplicationName))
                throw new ConfigurationErrorsException("web.config configuration error: LogConfiguration requires an 'applicationName' attribute");
            _applicationName = settings.ApplicationName;

            //-- Add the file appenders
            foreach (var fileAppender in settings.FileAppenders)
                root.AddAppender(ConfigureFileAppender(fileAppender));

            //-- Add the dB appenders
            foreach (var dbAppender in settings.DbAppenders)
                root.AddAppender(ConfigureDbAppender(dbAppender));

            root.Repository.Configured = true;
        }

        #region Configure Appenders
        private static IAppender ConfigureDbAppender(DbConfigurationElement dbAppender)
        {
            var name = dbAppender.Name;
            if (string.IsNullOrEmpty(name))
                throw new ConfigurationErrorsException("web.config configuration error, dbAppender requires a name");

            var connectionStringName = dbAppender.ConnectionStringName;
            if (string.IsNullOrEmpty(connectionStringName))
                throw new ConfigurationErrorsException("web.config configuration error, dbAppender requires a connectionStringName");

            var storedProcedureName = dbAppender.StoredProcedureName;
            if (string.IsNullOrEmpty(storedProcedureName))
                throw new ConfigurationErrorsException("web.config configuration error, dbAppender requires a storedProcedureName");

            var connectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ToString();
            if (string.IsNullOrEmpty(connectionString))
                throw new ConfigurationErrorsException(string.Format("web.config configuration error, connection string '{0}' is missing", connectionStringName));

            var minValue = ParseLevel(dbAppender.MinLevel, Level.Debug);
            var maxValue = ParseLevel(dbAppender.MaxLevel, Level.Fatal);

            return CreateAdoNetAppender(name, connectionString, storedProcedureName, new LevelRangeFilter() { LevelMin = minValue, LevelMax = maxValue });
        }

        private static IAppender ConfigureFileAppender(FileConfigurationElement fileAppender)
        {
            var name = fileAppender.Name;
            if (string.IsNullOrEmpty(name))
                throw new ConfigurationErrorsException("web.config configuration error, FileAppender requires a name");

            var path = fileAppender.Path;
            if (string.IsNullOrEmpty(path))
                path = @"\";

            var filename = fileAppender.FileName;
            if (string.IsNullOrEmpty(filename))
                filename = string.Format("{0}.txt", name);

            var filePath = Path.Combine(path, filename);
            var minValue = ParseLevel(fileAppender.MinLevel, Level.Debug);
            var maxValue = ParseLevel(fileAppender.MaxLevel, Level.Fatal);

            return CreateFileAppender(name, filePath, new LevelRangeFilter() { LevelMin = minValue, LevelMax = maxValue });
        }
        #endregion

        #region Appenders
        private static IAppender CreateFileAppender(string name, string filePath, IFilter levelFilter)
        {
            var layout = new PatternLayout(string.Format(@"Date:%date, Level:%p, Application:{0}, Class:%c, Thread:%t, Message:%m, Exception:%exception%n", _applicationName));
            var fileAppender = new RollingFileAppender
            {
                Name = name,
                Layout = layout,
                File = filePath,
                RollingStyle = RollingFileAppender.RollingMode.Size,
                MaxFileSize = 5242880,
                MaxSizeRollBackups = 3
            };
            fileAppender.AddFilter(levelFilter);
            fileAppender.ActivateOptions();
            return fileAppender;
        }

        public static IAppender CreateAdoNetAppender(string name, string connectionString, string spocName, IFilter levelFilter)
        {
            var adoNetAppender = new AdoNetAppender()
            {
                Name = name,
                BufferSize = 1,
                ConnectionType = "System.Data.SqlClient.SqlConnection, System.Data, Version=1.0.3300.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                ConnectionString = connectionString,
                CommandType = CommandType.StoredProcedure,
                CommandText = spocName,
                UseTransactions = false
            };

            //-- Create parameters
            adoNetAppender.AddParameter(CreateParameter("@DateCreated", DbType.DateTime, null));
            adoNetAppender.AddParameter(CreateParameter("@Application", DbType.String, new PatternLayout(_applicationName), 250));
            adoNetAppender.AddParameter(CreateParameter("@Level", DbType.String, new PatternLayout("%p")));
            adoNetAppender.AddParameter(CreateParameter("@Class", DbType.String, new PatternLayout("%c"), 250));
            adoNetAppender.AddParameter(CreateParameter("@Thread", DbType.Int32, new PatternLayout("%t")));
            adoNetAppender.AddParameter(CreateParameter("@Message", DbType.String, new PatternLayout("%m"), 8000));
            adoNetAppender.AddParameter(CreateParameter("@Exception", DbType.String, new PatternLayout("%exception"), 8000));
            adoNetAppender.AddParameter(CreateParameter("@Operation", DbType.String, new PatternLayout("InsertError"), 150));

            adoNetAppender.AddFilter(levelFilter);
            adoNetAppender.ActivateOptions();
            return adoNetAppender;
        }
        #endregion

        #region Private Methods
        private static AdoNetAppenderParameter CreateParameter(string parameterName, DbType dbType, ILayout patternLayout, int size)
        {
            var param = CreateParameter(parameterName, dbType, patternLayout);
            param.Size = size;
            return param;
        }

        private static AdoNetAppenderParameter CreateParameter(string parameterName, DbType dbType, ILayout patternLayout)
        {
            var param = new AdoNetAppenderParameter() { ParameterName = parameterName, DbType = dbType };

            if (dbType == DbType.DateTime)
                param.Layout = new RawTimeStampLayout();
            else
                param.Layout = new Layout2RawLayoutAdapter(patternLayout);

            return param;
        }

        private static Level ParseLevel(string level, Level defaultValue)
        {
            switch (level.Trim().ToLower())
            {
                case "debug":
                    return Level.Debug;
                case "info":
                    return Level.Info;
                case "warn":
                    return Level.Warn;
                case "error":
                    return Level.Error;
                case "fatal":
                    return Level.Fatal;
            }
            return defaultValue;
        }
        #endregion
    }
}
