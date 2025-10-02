using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using NLog;
using DisplayProfileManager.Core;

namespace DisplayProfileManager.Helpers
{
    public class AutoStartHelper
    {
        private static readonly Logger logger = LoggerHelper.GetLogger();

        // Registry constants
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RegistryValueName = "DisplayProfileManager";

        // Task Scheduler constants
        private const string TaskName = "DisplayProfileManager_Startup";
        private const string TaskFolder = "\\DisplayProfileManager";
        private const string FullTaskPath = TaskFolder + "\\" + TaskName;

        #region Public Methods

        public bool IsAutoStartEnabled(AutoStartMode? checkMode = null)
        {
            try
            {
                if (checkMode.HasValue)
                {
                    return checkMode.Value == AutoStartMode.Registry
                        ? IsAutoStartEnabledRegistry()
                        : IsAutoStartEnabledTaskScheduler();
                }

                // Check both methods
                return IsAutoStartEnabledRegistry() || IsAutoStartEnabledTaskScheduler();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking auto start status: {ex.Message}");
                logger.Error(ex, "Error checking auto start status");
                return false;
            }
        }

        public bool EnableAutoStart(AutoStartMode mode, bool startInTray = false)
        {
            try
            {
                return mode == AutoStartMode.Registry
                    ? EnableAutoStartRegistry(startInTray)
                    : EnableAutoStartTaskScheduler(startInTray);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enabling auto start: {ex.Message}");
                logger.Error(ex, "Error enabling auto start");
                return false;
            }
        }

        public bool DisableAutoStart()
        {
            try
            {
                bool registryResult = true;
                bool taskSchedulerResult = true;

                // Disable BOTH methods to ensure clean state
                if (IsAutoStartEnabledRegistry())
                {
                    registryResult = DisableAutoStartRegistry();
                }
                
                if(IsAutoStartEnabledTaskScheduler())
                {
                    taskSchedulerResult = DisableAutoStartTaskScheduler();
                }

                return registryResult || taskSchedulerResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disabling auto start: {ex.Message}");
                logger.Error(ex, "Error disabling auto start");
                return false;
            }
        }

        public bool ValidateAutoStartEntry(AutoStartMode mode)
        {
            try
            {
                return mode == AutoStartMode.Registry
                    ? ValidateAutoStartEntryRegistry()
                    : ValidateAutoStartEntryTaskScheduler();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating auto start entry: {ex.Message}");
                logger.Error(ex, "Error validating auto start entry");
                return false;
            }
        }

        public string GetAutoStartCommand(AutoStartMode mode)
        {
            try
            {
                return mode == AutoStartMode.Registry
                    ? GetAutoStartCommandRegistry()
                    : GetAutoStartCommandTaskScheduler();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting auto start command: {ex.Message}");
                logger.Error(ex, "Error getting auto start command");
                return string.Empty;
            }
        }

        public AutoStartInfo GetAutoStartInfo(AutoStartMode mode)
        {
            return mode == AutoStartMode.Registry
                ? GetAutoStartInfoRegistry()
                : GetAutoStartInfoTaskScheduler();
        }

        #endregion

        #region Registry Implementation

        private bool IsAutoStartEnabledRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(RegistryValueName);
                        bool isEnabled = value != null;

                        System.Diagnostics.Debug.WriteLine($"Auto-start registry value {(isEnabled ? "found" : "not found")}");
                        logger.Debug($"Auto-start registry value {(isEnabled ? "found" : "not found")}");

                        return isEnabled;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking registry auto start: {ex.Message}");
                logger.Error(ex, "Error checking registry auto start");
                return false;
            }
        }

        private bool EnableAutoStartRegistry(bool startInTray = false)
        {
            try
            {
                var executablePath = GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath))
                {
                    System.Diagnostics.Debug.WriteLine("Could not determine executable path");
                    logger.Error("Could not determine executable path");
                    return false;
                }

                if (!File.Exists(executablePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Executable path does not exist: {executablePath}");
                    logger.Error($"Executable path does not exist: {executablePath}");
                    return false;
                }

                // Build the command with optional --tray argument
                var command = startInTray ? $"\"{executablePath}\" --tray" : $"\"{executablePath}\"";

                // Write to registry
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (key != null)
                    {
                        key.SetValue(RegistryValueName, command, RegistryValueKind.String);

                        System.Diagnostics.Debug.WriteLine($"Successfully enabled registry auto-start: {command}");
                        logger.Info($"Successfully enabled registry auto-start: {command}");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Could not open registry key for writing");
                        logger.Error("Could not open registry key for writing");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enabling registry auto start: {ex.Message}");
                logger.Error(ex, "Error enabling registry auto start");
                return false;
            }
        }

        private bool DisableAutoStartRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(RegistryValueName);
                        if (value != null)
                        {
                            key.DeleteValue(RegistryValueName, false);
                            System.Diagnostics.Debug.WriteLine("Successfully disabled registry auto-start");
                            logger.Info("Successfully disabled registry auto-start");
                        }
                        return true;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disabling registry auto start: {ex.Message}");
                logger.Error(ex, "Error disabling registry auto start");
                return false;
            }
        }

        private bool ValidateAutoStartEntryRegistry()
        {
            try
            {
                if (!IsAutoStartEnabledRegistry())
                    return false;

                var currentCommand = GetAutoStartCommandRegistry();
                var currentExePath = GetExecutablePath();

                if (string.IsNullOrEmpty(currentCommand) || string.IsNullOrEmpty(currentExePath))
                    return false;

                var normalizedCommand = currentCommand.Replace("\"", "").ToLowerInvariant();
                var normalizedExePath = currentExePath.ToLowerInvariant();

                return normalizedCommand.StartsWith(normalizedExePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating registry auto start: {ex.Message}");
                logger.Error(ex, "Error validating registry auto start");
                return false;
            }
        }

        private string GetAutoStartCommandRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(RegistryValueName);
                        if (value != null)
                        {
                            return value.ToString();
                        }
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting registry auto start command: {ex.Message}");
                logger.Error(ex, "Error getting registry auto start command");
                return string.Empty;
            }
        }

        private AutoStartInfo GetAutoStartInfoRegistry()
        {
            var info = new AutoStartInfo
            {
                IsEnabled = IsAutoStartEnabledRegistry(),
                Command = GetAutoStartCommandRegistry(),
                ExecutablePath = GetExecutablePath(),
                IsValid = ValidateAutoStartEntryRegistry(),
                TaskStatus = IsAutoStartEnabledRegistry() ? "Enabled" : "Disabled"
            };

            return info;
        }

        #endregion

        #region Task Scheduler Implementation

        private bool IsAutoStartEnabledTaskScheduler()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Query /TN \"{FullTaskPath}\" /FO CSV",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                bool isEnabled = process.ExitCode == 0 && output.Contains(TaskName);

                System.Diagnostics.Debug.WriteLine($"Task Scheduler auto-start {(isEnabled ? "found" : "not found")}");
                logger.Debug($"Task Scheduler auto-start {(isEnabled ? "found" : "not found")}");

                return isEnabled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking Task Scheduler auto start: {ex.Message}");
                logger.Error(ex, "Error checking Task Scheduler auto start");
                return false;
            }
        }

        private bool EnableAutoStartTaskScheduler(bool startInTray = false)
        {
            try
            {
                var executablePath = GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath))
                {
                    System.Diagnostics.Debug.WriteLine("Could not determine executable path");
                    logger.Error("Could not determine executable path");
                    return false;
                }

                if (!File.Exists(executablePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Executable path does not exist: {executablePath}");
                    logger.Error($"Executable path does not exist: {executablePath}");
                    return false;
                }

                // Generate XML for the task
                var xmlContent = GenerateTaskXml(executablePath, startInTray);
                var tempXmlPath = Path.Combine(Path.GetTempPath(), "DisplayProfileManager_Task.xml");

                try
                {
                    File.WriteAllText(tempXmlPath, xmlContent, Encoding.Unicode);

                    bool isAdmin = IsRunningAsAdmin();
                    Process process;

                    process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "schtasks.exe",
                            Arguments = $"/Create /TN \"{FullTaskPath}\" /XML \"{tempXmlPath}\" /F",
                            UseShellExecute = true,
                            Verb = "runas", // This triggers UAC prompt
                            CreateNoWindow = false // Must be false when using ShellExecute
                        }
                    };

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("Successfully created Task Scheduler auto-start (elevated)");
                        logger.Info("Successfully created Task Scheduler auto-start (elevated)");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to create Task Scheduler auto-start (elevated). Exit code: {process.ExitCode}");
                        logger.Error($"Failed to create Task Scheduler auto-start (elevated). Exit code: {process.ExitCode}");
                        return false;
                    }
                }
                finally
                {
                    if (File.Exists(tempXmlPath))
                    {
                        try { File.Delete(tempXmlPath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enabling Task Scheduler auto start: {ex.Message}");
                logger.Error(ex, "Error enabling Task Scheduler auto start");
                return false;
            }
        }

        private bool DisableAutoStartTaskScheduler()
        {
            try
            {
                bool isAdmin = IsRunningAsAdmin();
                Process process;

                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Delete /TN \"{FullTaskPath}\" /F",
                        UseShellExecute = true,
                        Verb = "runas", // This triggers UAC prompt
                        CreateNoWindow = false
                    }
                };

                process.Start();
                process.WaitForExit();

                // When elevated, we can't easily check if task didn't exist
                // Exit code 0 means success or task didn't exist
                if (process.ExitCode == 0)
                {
                    System.Diagnostics.Debug.WriteLine("Successfully deleted Task Scheduler auto-start (elevated)");
                    logger.Info("Successfully deleted Task Scheduler auto-start (elevated)");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete Task Scheduler auto-start (elevated). Exit code: {process.ExitCode}");
                    logger.Error($"Failed to delete Task Scheduler auto-start (elevated). Exit code: {process.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disabling Task Scheduler auto start: {ex.Message}");
                logger.Error(ex, "Error disabling Task Scheduler auto start");
                return false;
            }
        }

        private bool ValidateAutoStartEntryTaskScheduler()
        {
            try
            {
                if (!IsAutoStartEnabledTaskScheduler())
                    return false;

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Query /TN \"{FullTaskPath}\" /FO LIST /V",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    return output.Contains("Enabled") && !output.Contains("Disabled");
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating Task Scheduler auto start: {ex.Message}");
                logger.Error(ex, "Error validating Task Scheduler auto start");
                return false;
            }
        }

        private string GetAutoStartCommandTaskScheduler()
        {
            try
            {
                if (!IsAutoStartEnabledTaskScheduler())
                    return string.Empty;

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Query /TN \"{FullTaskPath}\" /FO LIST /V",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("Task To Run:"))
                        {
                            var parts = line.Split(':');
                            if (parts.Length > 1)
                            {
                                return parts[1].Trim();
                            }
                        }
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting Task Scheduler auto start command: {ex.Message}");
                logger.Error(ex, "Error getting Task Scheduler auto start command");
                return string.Empty;
            }
        }

        private AutoStartInfo GetAutoStartInfoTaskScheduler()
        {
            var info = new AutoStartInfo
            {
                IsEnabled = IsAutoStartEnabledTaskScheduler(),
                Command = GetAutoStartCommandTaskScheduler(),
                ExecutablePath = GetExecutablePath(),
                IsValid = ValidateAutoStartEntryTaskScheduler()
            };

            if (info.IsEnabled)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "schtasks.exe",
                            Arguments = $"/Query /TN \"{FullTaskPath}\" /FO LIST /V",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        var lines = output.Split('\n');
                        foreach (var line in lines)
                        {
                            if (line.Contains("Status:"))
                            {
                                info.TaskStatus = line.Split(':')[1].Trim();
                            }
                            else if (line.Contains("Last Run Time:"))
                            {
                                var timeStr = line.Substring(line.IndexOf(':') + 1).Trim();
                                if (DateTime.TryParse(timeStr, out var lastRun))
                                {
                                    info.LastRunTime = lastRun;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting Task Scheduler info: {ex.Message}");
                    logger.Error(ex, "Error getting Task Scheduler info");
                }
            }

            return info;
        }

        private string GenerateTaskXml(string executablePath, bool startInTray = false)
        {
            var currentUser = Environment.UserDomainName + "\\" + Environment.UserName;
            var timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            var description = startInTray ? "Starts Display Profile Manager minimized to system tray when user logs on" : "Starts Display Profile Manager when user logs on";
            var argumentsElement = startInTray ? $"\n      <Arguments>--tray</Arguments>" : "";

            string xmlText = 
                $@"<?xml version=""1.0"" encoding=""UTF-16""?>
                <Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
                    <RegistrationInfo>
                    <Date>{timestamp}</Date>
                    <Author>{currentUser}</Author>
                    <Description>{description}</Description>
                    <URI>{FullTaskPath}</URI>
                    </RegistrationInfo>
                    <Triggers>
                    <LogonTrigger>
                        <Enabled>true</Enabled>
                        <UserId>{currentUser}</UserId>
                    </LogonTrigger>
                    </Triggers>
                    <Principals>
                    <Principal id=""Author"">
                        <UserId>{currentUser}</UserId>
                        <LogonType>InteractiveToken</LogonType>
                        <RunLevel>LeastPrivilege</RunLevel>
                    </Principal>
                    </Principals>
                    <Settings>
                    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                    <AllowHardTerminate>true</AllowHardTerminate>
                    <StartWhenAvailable>false</StartWhenAvailable>
                    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                    <IdleSettings>
                        <StopOnIdleEnd>false</StopOnIdleEnd>
                        <RestartOnIdle>false</RestartOnIdle>
                    </IdleSettings>
                    <AllowStartOnDemand>true</AllowStartOnDemand>
                    <Enabled>true</Enabled>
                    <Hidden>false</Hidden>
                    <RunOnlyIfIdle>false</RunOnlyIfIdle>
                    <DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession>
                    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
                    <WakeToRun>false</WakeToRun>
                    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                    <Priority>0</Priority>
                    </Settings>
                    <Actions Context=""Author"">
                    <Exec>
                        <Command>{executablePath}</Command>{argumentsElement}
                        <WorkingDirectory>{Path.GetDirectoryName(executablePath)}</WorkingDirectory>
                    </Exec>
                    </Actions>
                </Task>";

            return xmlText;
        }

        #endregion

        #region Helper Methods

        private string GetExecutablePath()
        {
            try
            {
                var processPath = Process.GetCurrentProcess().MainModule.FileName;

                if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                {
                    return processPath;
                }

                var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(assemblyLocation) && File.Exists(assemblyLocation))
                {
                    return assemblyLocation;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting executable path: {ex.Message}");
                logger.Error(ex, "Error getting executable path");
                return string.Empty;
            }
        }

        public static bool IsRunningAsAdmin()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking admin status: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

    public class AutoStartInfo
    {
        public bool IsEnabled { get; set; }
        public string Command { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string TaskStatus { get; set; } = string.Empty;
        public DateTime? LastRunTime { get; set; }

        public override string ToString()
        {
            return $"Enabled: {IsEnabled}, Valid: {IsValid}, Status: {TaskStatus}, Command: {Command}, LastRun: {LastRunTime}";
        }
    }
}
