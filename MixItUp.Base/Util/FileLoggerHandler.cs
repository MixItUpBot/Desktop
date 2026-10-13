using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace MixItUp.Base.Util
{
    public static class FileLoggerHandler
    {
        public static string CurrentLogFilePath { get; private set; }
        public static string CurrentChatEventLogFilePath { get; private set; }

        private const string LogsDirectoryName = "Logs";
        private const string LogFileNameFormat = "MixItUpLog-{0}.txt";

        private static readonly object logLock = new object();
        private static Task pendingWrite = Task.CompletedTask;

        public static void Flush()
        {
            Task write;
            lock (logLock) { write = pendingWrite; }
            write.GetAwaiter().GetResult();
        }

        public static void Initialize()
        {
            ServiceManager.Get<IFileService>().CreateDirectory(LogsDirectoryName);
            FileLoggerHandler.CurrentLogFilePath = Path.Combine(LogsDirectoryName, string.Format(LogFileNameFormat, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture)));

            Logger.LogOccurred += Logger_LogOccurred;
        }

        private static void Logger_LogOccurred(object sender, Log log)
        {
            string message = string.Format("{0} - {1} - {2} " + Environment.NewLine + Environment.NewLine,
                DateTimeOffset.Now.ToString(), EnumHelper.GetEnumName(log.Level), log.Message);
            lock (logLock)
            {
                // Keep writes ordered and independent of the startup dispatcher so Flush can wait.
                pendingWrite = pendingWrite.ContinueWith(async _ =>
                {
                    try
                    {
                        await ServiceManager.Get<IFileService>().AppendFile(FileLoggerHandler.CurrentLogFilePath, message);
                    }
                    catch (Exception) { }
                }, TaskScheduler.Default).Unwrap();
            }
        }
    }
}
