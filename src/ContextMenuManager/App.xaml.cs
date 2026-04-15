using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using ContextMenuManager.Services;

namespace ContextMenuManager;

public partial class App : Application
{
    private static IServiceProvider? _services;

    public static IServiceProvider Services => _services 
        ?? throw new InvalidOperationException("Services not initialized");

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length >= 3 && e.Args[0] == "--create-file")
        {
            CreateFileSilently(e.Args[1], e.Args[2]);
            Shutdown();
            return;
        }

        if (e.Args.Length >= 2 && e.Args[0] == "--elevate-cmd")
        {
            var path = e.Args.Length > 1 ? e.Args[1] : "";
            ElevateCmd(path);
            Shutdown();
            return;
        }

        if (e.Args.Length >= 2 && e.Args[0] == "--open-cmd")
        {
            var path = e.Args.Length > 1 ? e.Args[1] : "";
            OpenCmd(path);
            Shutdown();
            return;
        }
        
        if (!IsRunningAsAdmin())
        {
            RestartAsAdmin();
            Shutdown();
            return;
        }

        var splash = new SplashWindow();
        splash.Show();

        _services = AppServices.ConfigureServices();
        
        var logger = _services.GetRequiredService<ILoggingService>();
        logger.LogInfo("Application launching...");
        logger.LogInfo("Running as administrator");

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        var mainWindow = new MainWindow();
        mainWindow.Show();
        
        splash.Close();
        
        ActivateWindow(mainWindow);
    }

    private void CreateFileSilently(string dirPath, string extension)
    {
        try
        {
            var dir = dirPath;
            if (string.IsNullOrEmpty(dir))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (dir.Length == 2 && dir[1] == ':')
                dir = dir + "\\";

            if (dir.EndsWith("\\"))
                dir = dir.TrimEnd('\\');

            var filePath = Path.Combine(dir, "New" + extension);
            if (!File.Exists(filePath))
                File.WriteAllText(filePath, string.Empty);
        }
        catch { }
    }

    private void ElevateCmd(string dirPath)
    {
        try
        {
            var dir = dirPath;
            if (string.IsNullOrEmpty(dir))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (dir.Length == 2 && dir[1] == ':')
                dir = dir + "\\";

            if (dir.EndsWith("\\"))
                dir = dir.TrimEnd('\\');

            if (!System.IO.Directory.Exists(dir))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = "cmd.exe",
                Arguments = $"/k \"cd /d \"{dir}\"\"",
                Verb = "runas"
            };
            Process.Start(startInfo);
        }
        catch { }
    }

    private void OpenCmd(string dirPath)
    {
        try
        {
            var dir = dirPath;
            if (string.IsNullOrEmpty(dir))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (dir.Length == 2 && dir[1] == ':')
                dir = dir + "\\";

            if (dir.EndsWith("\\"))
                dir = dir.TrimEnd('\\');

            if (!System.IO.Directory.Exists(dir))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = "cmd.exe",
                Arguments = $"/k \"cd /d \"{dir}\"\""
            };
            Process.Start(startInfo);
        }
        catch { }
    }

    private void ActivateWindow(Window window)
    {
        window.WindowState = WindowState.Normal;
        window.Topmost = true;
        window.Activate();
        window.Topmost = false;
        window.Focus();
        
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            SetForegroundWindow(handle);
            ShowWindow(handle, SW_RESTORE);
        }
    }

    private bool IsRunningAsAdmin()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private void RestartAsAdmin()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName,
                Verb = "runas"
            };

            Process.Start(processInfo);
        }
        catch
        {
        }
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var logger = _services?.GetRequiredService<ILoggingService>();
        logger?.LogError($"Unhandled UI exception: {e.Exception}");
        
        MessageBox.Show(
            $"An unexpected error occurred:\n{e.Exception.Message}",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var logger = _services?.GetRequiredService<ILoggingService>();
        if (e.ExceptionObject is Exception ex)
        {
            logger?.LogError($"Unhandled domain exception: {ex}");
        }
    }
}
