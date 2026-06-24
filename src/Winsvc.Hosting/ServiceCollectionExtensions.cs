using System.Runtime.Versioning;
using Microsoft.Extensions.Hosting;
using Winsvc.Core;
using Winsvc.Infrastructure;

namespace Winsvc.Hosting;

[SupportedOSPlatform("windows")]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWinsvcServices(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        string? contentRootPath = null)
    {
        services.AddSingleton<IManifestReader, YamlManifestReader>();
        services.AddSingleton<IManifestValidator, ManifestValidator>();
        services.AddSingleton<FileSystemWatcherManifestProvider>(sp =>
        {
            var manifestDirectory = ManifestPathResolver.ResolveDirectory(
                configuration?["Winsvc:ManifestDirectory"],
                contentRootPath ?? AppContext.BaseDirectory,
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory());
            var hotReloadEnabled = configuration?.GetValue<bool?>("Winsvc:ManifestHotReload") ?? true;
            var logger = sp.GetRequiredService<ILogger<FileSystemWatcherManifestProvider>>();

            return new FileSystemWatcherManifestProvider(
                sp.GetRequiredService<IManifestReader>(),
                sp.GetRequiredService<IManifestValidator>(),
                manifestDirectory,
                hotReloadEnabled,
                logger);
        });
        services.AddSingleton<IManifestProvider>(sp => sp.GetRequiredService<FileSystemWatcherManifestProvider>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<FileSystemWatcherManifestProvider>());
        services.AddSingleton<IServiceConfigGenerator, WinSwXmlGenerator>();
        services.AddSingleton<IHealthChecker, HttpClientHealthChecker>();
        services.AddSingleton<IWindowsServiceMonitor, WindowsServiceMonitor>();
        services.AddSingleton<IServiceManager, WinSwServiceManager>();
        return services;
    }
}
