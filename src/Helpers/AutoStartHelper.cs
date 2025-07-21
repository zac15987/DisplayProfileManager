using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Win32;

namespace DisplayProfileManager.Helpers
{
    public class AutoStartHelper
    {
        private const string TaskName = "DisplayProfileManager_Startup";
        private const string TaskFolder = "\\DisplayProfileManager";
        private const string FullTaskPath = TaskFolder + "\\" + TaskName;
        
        // Legacy registry path for migration
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string RegistryAppName = "DisplayProfileManager";

        public bool IsAutoStartEnabled()
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
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && output.Contains(TaskName))
                {
                    System.Diagnostics.Debug.WriteLine($"Task found: {FullTaskPath}");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"Task not found or error: {error}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking auto start status: {ex.Message}");
                return false;
            }
        }

        public bool EnableAutoStart()
        {
            try
            {
                var executablePath = GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath))
                {
                    System.Diagnostics.Debug.WriteLine("Could not determine executable path");
                    return false;
                }

                if (!File.Exists(executablePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Executable path does not exist: {executablePath}");
                    return false;
                }

                // Create task folder if it doesn't exist
                CreateTaskFolder();

                // Generate XML for the task
                var xmlContent = GenerateTaskXml(executablePath);
                var tempXmlPath = Path.Combine(Path.GetTempPath(), "DisplayProfileManager_Task.xml");
                
                try
                {
                    File.WriteAllText(tempXmlPath, xmlContent, Encoding.Unicode);

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "schtasks.exe",
                            Arguments = $"/Create /TN \"{FullTaskPath}\" /XML \"{tempXmlPath}\" /F",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Successfully created scheduled task: {output}");
                        MigrateFromRegistry(); // Clean up old registry entry if exists
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to create scheduled task. Error: {error}");
                        return false;
                    }
                }
                finally
                {
                    // Clean up temp file
                    if (File.Exists(tempXmlPath))
                    {
                        try { File.Delete(tempXmlPath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enabling auto start: {ex.Message}");
                return false;
            }
        }

        public bool DisableAutoStart()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Delete /TN \"{FullTaskPath}\" /F",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Successfully deleted scheduled task: {output}");
                    return true;
                }
                else if (error.Contains("ERROR: The system cannot find the file specified"))
                {
                    // Task doesn't exist, which is fine for disable operation
                    System.Diagnostics.Debug.WriteLine("Task doesn't exist, nothing to delete");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete scheduled task. Error: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disabling auto start: {ex.Message}");
                return false;
            }
        }

        private string GetExecutablePath()
        {
            try
            {
                // Primary method: Use Process.GetCurrentProcess() which returns proper Windows path
                var processPath = Process.GetCurrentProcess().MainModule.FileName;
                
                // Validate the path exists
                if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Executable path found: {processPath}");
                    return processPath;
                }
                
                // Fallback: Try using Assembly.Location
                var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(assemblyLocation) && File.Exists(assemblyLocation))
                {
                    System.Diagnostics.Debug.WriteLine($"Using assembly location: {assemblyLocation}");
                    return assemblyLocation;
                }
                
                System.Diagnostics.Debug.WriteLine("No valid executable path found");
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting executable path: {ex.Message}");
                return string.Empty;
            }
        }

        private void CreateTaskFolder()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Create /TN \"{TaskFolder}\\dummy\" /SC ONCE /ST 00:00 /TR \"cmd /c echo\" /F",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();

                // Delete the dummy task
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Delete /TN \"{TaskFolder}\\dummy\" /F",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating task folder: {ex.Message}");
            }
        }

        private string GenerateTaskXml(string executablePath)
        {
            var currentUser = Environment.UserDomainName + "\\" + Environment.UserName;
            var timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Date>{timestamp}</Date>
    <Author>{currentUser}</Author>
    <Description>Starts Display Profile Manager when user logs on</Description>
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
      <RunLevel>HighestAvailable</RunLevel>
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
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{executablePath}</Command>
      <WorkingDirectory>{Path.GetDirectoryName(executablePath)}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";
        }

        private void MigrateFromRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(RegistryAppName);
                        if (value != null)
                        {
                            key.DeleteValue(RegistryAppName, false);
                            System.Diagnostics.Debug.WriteLine("Migrated from registry to Task Scheduler");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during registry migration: {ex.Message}");
            }
        }

        public bool ValidateAutoStartEntry()
        {
            try
            {
                if (!IsAutoStartEnabled())
                    return false;

                // Query task status
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
                    // Check if task is enabled
                    return output.Contains("Enabled") && !output.Contains("Disabled");
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating auto start entry: {ex.Message}");
                return false;
            }
        }

        public bool RefreshAutoStartEntry()
        {
            try
            {
                if (IsAutoStartEnabled())
                {
                    DisableAutoStart();
                    return EnableAutoStart();
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing auto start entry: {ex.Message}");
                return false;
            }
        }

        public string GetAutoStartCommand()
        {
            try
            {
                if (!IsAutoStartEnabled())
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
                    // Extract task action from output
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
                System.Diagnostics.Debug.WriteLine($"Error getting auto start command: {ex.Message}");
                return string.Empty;
            }
        }

        public AutoStartInfo GetAutoStartInfo()
        {
            var info = new AutoStartInfo
            {
                IsEnabled = IsAutoStartEnabled(),
                Command = GetAutoStartCommand(),
                ExecutablePath = GetExecutablePath(),
                IsValid = ValidateAutoStartEntry()
            };

            // Get additional task information if enabled
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
                    System.Diagnostics.Debug.WriteLine($"Error getting task info: {ex.Message}");
                }
            }

            return info;
        }
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