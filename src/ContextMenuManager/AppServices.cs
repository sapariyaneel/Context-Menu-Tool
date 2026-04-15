using Microsoft.Extensions.DependencyInjection;
using ContextMenuManager.Services;

namespace ContextMenuManager;

public static class AppServices
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IRegistryService, RegistryService>();
        services.AddSingleton<IElevationService, ElevationService>();
        services.AddSingleton<IContextMenuService, ContextMenuService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IInstalledAppsService, InstalledAppsService>();
        services.AddSingleton<NewSubmenuService>();
        services.AddSingleton<OpenInCmdService>();
        
        return services.BuildServiceProvider();
    }

    public static T GetService<T>() where T : class
    {
        return App.Services.GetRequiredService<T>();
    }
}
