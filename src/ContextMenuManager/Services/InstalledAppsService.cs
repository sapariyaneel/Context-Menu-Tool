using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;

namespace ContextMenuManager.Services;

public class InstalledApp
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
}

public interface IInstalledAppsService
{
    List<InstalledApp> GetInstalledApps();
}

public class InstalledAppsService : IInstalledAppsService
{
    private readonly ILoggingService _logger;

    private static readonly string[] ExcludeNamePatterns = new[]
    {
        "runtime", "redist", " Redist", "组件",
        ".NET SDK", "Build Tools", "Windows SDK",
        "Windows Defender", "Windows Security",
        "WebView2", "EdgeUpdate",
        "Azure", "ReportViewer", "SSMS",
        "WMF", "PSModule", "WindowsPowerShell",
        "VCRedist", "vcredist", "vc_redist", "Visual C++",
        "Hotfix", "KB", "Patch", "Update for Windows",
        "Language Pack",
        "Git Bash", "Git CMD",
        "Blend for Visual Studio",
        "Visual Studio Installer",
        "Docker", "Kubernetes",
        "Winget", "winget",
        "Driver", "Realtek",
        "Live Captions",
        "Python", "Anaconda", "Miniconda",
        "JDK", "JRE", "Java",
        "Node.js", "npm", "TypeScript",
        "Ruby", "Go", "Rust",
        "MySQL", "PostgreSQL", "MongoDB", "SQLite",
        "IntelliJ", "PyCharm", "WebStorm", "Rider",
        "VirtualBox", "VMware", "Hyper-V",
        "DirectX", "DirectPlay",
    };

    private static readonly string[] ExcludeExePatterns = new[]
    {
        "unins", "uninst", "uninstall",
        "setup", "update", "upgrade",
        "install", "installer",
        "crash", "helper", "service",
        "background", "daemon",
        "monitor", "tray",
        "register", "reg", "unreg",
        "activate", "license",
        "config", "settings",
    };

    private static readonly string[] ExcludePathPatterns = new[]
    {
        @"\windows\system32",
        @"\windows\syswow64",
        @"\windows\winsxs",
        @"\windows\package list",
        @"\programdata\package cache",
        @"\reference assemblies",
        @"\windows kits\",
        @"\windows\installer\",
        @"\temp\",
        @"\tmp\",
        @"\downloads\",
    };

    [ComImport]
    [Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")]
    private class WshShell { }

    [ComImport]
    [Guid("F935DC21-1CF0-11D0-ADB9-00C04FD58A0B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    private interface IWshShortcut
    {
        string TargetPath { get; }
    }

    public InstalledAppsService(ILoggingService logger)
    {
        _logger = logger;
    }

    public List<InstalledApp> GetInstalledApps()
    {
        var result = new List<InstalledApp>();
        var seenExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        GetAppsFromShortcuts(result, seenExes);
        GetAppsFromRegistry(result, seenExes);

        var finalResult = result
            .Where(a => !string.IsNullOrWhiteSpace(a.ExecutablePath) && 
                       File.Exists(a.ExecutablePath) &&
                       !string.IsNullOrWhiteSpace(a.DisplayName) &&
                       a.DisplayName.Length >= 3 &&
                       !HasExcludedPattern(a.ExecutablePath) &&
                       !ShouldExcludeByName(a.DisplayName))
            .GroupBy(a => a.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(a => a.DisplayName)
            .ToList();

        _logger.LogInfo($"Total apps found: {finalResult.Count}");

        return finalResult;
    }

    private bool HasExcludedPattern(string path)
    {
        var lower = path.ToLowerInvariant();
        
        foreach (var pattern in ExcludePathPatterns)
        {
            if (lower.Contains(pattern))
                return true;
        }

        var exeName = Path.GetFileName(lower);
        foreach (var pattern in ExcludeExePatterns)
        {
            if (exeName.Contains(pattern))
                return true;
        }

        return false;
    }

    private bool ShouldExcludeByName(string displayName)
    {
        var lower = displayName.ToLowerInvariant();
        
        foreach (var pattern in ExcludeNamePatterns)
        {
            if (lower.Contains(pattern.ToLowerInvariant()))
                return true;
        }
        
        if (lower.StartsWith("update for") || lower.StartsWith("security update"))
            return true;

        if (lower.StartsWith("kb") && lower.Length <= 10)
            return true;

        return false;
    }

    private void GetAppsFromShortcuts(List<InstalledApp> result, HashSet<string> seenExes)
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };

        foreach (var folder in folders)
        {
            if (Directory.Exists(folder))
            {
                ScanFolderRecursive(folder, result, seenExes);
            }
        }
    }

    private void ScanFolderRecursive(string folder, List<InstalledApp> result, HashSet<string> seenExes)
    {
        try
        {
            foreach (var file in Directory.GetFiles(folder, "*.lnk"))
            {
                try
                {
                    var target = GetShortcutTarget(file);
                    if (string.IsNullOrWhiteSpace(target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var name = Path.GetFileNameWithoutExtension(file);
                    
                    if (name.Length < 3) continue;
                    if (ShouldExcludeByName(name)) continue;
                    if (HasExcludedPattern(target)) continue;

                    if (seenExes.Add(target))
                    {
                        result.Add(new InstalledApp
                        {
                            Name = name,
                            DisplayName = name,
                            ExecutablePath = target,
                            IconPath = target,
                            Publisher = string.Empty
                        });
                    }
                }
                catch { }
            }

            foreach (var subDir in Directory.GetDirectories(folder))
            {
                try
                {
                    var subDirName = Path.GetFileName(subDir);
                    if (subDirName.StartsWith(".")) continue;
                    
                    ScanFolderRecursive(subDir, result, seenExes);
                }
                catch { }
            }
        }
        catch { }
    }

    private void GetAppsFromRegistry(List<InstalledApp> result, HashSet<string> seenExes)
    {
        var hives = new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
        var views = new[] { RegistryView.Registry64, RegistryView.Registry32 };
        var paths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var hive in hives)
        {
            foreach (var view in views)
            {
                foreach (var path in paths)
                {
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                        using var uninstallKey = baseKey.OpenSubKey(path);
                        
                        if (uninstallKey == null) continue;

                        foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                        {
                            try
                            {
                                using var subKey = uninstallKey.OpenSubKey(subKeyName);
                                if (subKey == null) continue;

                                var displayName = subKey.GetValue("DisplayName") as string;
                                if (string.IsNullOrWhiteSpace(displayName)) continue;
                                if (displayName.Length < 3) continue;
                                if (ShouldExcludeByName(displayName)) continue;

                                var installLocation = subKey.GetValue("InstallLocation") as string;
                                var displayIcon = subKey.GetValue("DisplayIcon") as string;
                                var publisher = subKey.GetValue("Publisher") as string;
                                var noModify = subKey.GetValue("NoModify");
                                var systemComponent = subKey.GetValue("SystemComponent");

                                if (noModify != null || systemComponent != null) continue;

                                string? exePath = null;

                                if (!string.IsNullOrWhiteSpace(displayIcon))
                                {
                                    var iconPath = displayIcon.Split(',')[0].Trim('"').Trim();
                                    if (iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(iconPath))
                                    {
                                        if (!HasExcludedPattern(iconPath))
                                        {
                                            exePath = iconPath;
                                        }
                                    }
                                }

                                if (string.IsNullOrWhiteSpace(exePath) && !string.IsNullOrWhiteSpace(installLocation))
                                {
                                    if (Directory.Exists(installLocation))
                                    {
                                        var exeFiles = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly)
                                            .Where(f => !HasExcludedPattern(f) &&
                                                       !Path.GetFileName(f).StartsWith(".") &&
                                                       Path.GetFileName(f).Length >= 3)
                                            .Take(3)
                                            .ToList();

                                        exePath = exeFiles.FirstOrDefault();
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(exePath) && seenExes.Add(exePath))
                                {
                                    result.Add(new InstalledApp
                                    {
                                        Name = subKeyName,
                                        DisplayName = displayName,
                                        ExecutablePath = exePath,
                                        IconPath = displayIcon ?? exePath,
                                        Publisher = publisher ?? string.Empty
                                    });
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
        }
    }

    private string? GetShortcutTarget(string shortcutPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return null;
            
            dynamic? shell = null;
            dynamic? shortcut = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                shortcut = shell.CreateShortcut(shortcutPath);
                return shortcut.TargetPath as string;
            }
            finally
            {
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }
        catch
        {
            return null;
        }
    }
}
