using NLog;
using System.Runtime.CompilerServices;

namespace DisplayProfileManager.Helpers
{
    /// <summary>
    /// Centralized logging helper for easy NLog access throughout the application.
    /// </summary>
    public static class LoggerHelper
    {
        /// <summary>
        /// Gets a logger instance for the calling class.
        /// When called without parameters, automatically uses the caller's file name.
        /// When called with a string parameter, uses that as the logger name.
        /// </summary>
        /// <param name="callerFilePath">Logger name or automatically populated with the source file path</param>
        /// <returns>NLog Logger instance configured for the calling class</returns>
        public static Logger GetLogger([CallerFilePath] string callerFilePath = "")
        {
            // Extract class name from file path (e.g., "ProfileManager.cs" -> "ProfileManager")
            var className = System.IO.Path.GetFileNameWithoutExtension(callerFilePath);
            return LogManager.GetLogger(className);
        }
    }
}
