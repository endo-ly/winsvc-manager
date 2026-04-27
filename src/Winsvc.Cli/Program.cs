using System;
using System.CommandLine;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Winsvc.Contracts;
using Winsvc.Contracts.Manifest;
using Winsvc.Core;
using Winsvc.Hosting;
using Winsvc.Infrastructure;

namespace Winsvc.Cli;

[SupportedOSPlatform("windows")]
class Program
{
    static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        ConfigureServices(builder.Services);
        using var host = builder.Build();

        return await BuildCommandLine(host.Services).InvokeAsync(args);
    }

    [SupportedOSPlatform("windows")]
    static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IManifestReader, YamlManifestReader>();
        services.AddSingleton<IManifestValidator, ManifestValidator>();
        services.AddSingleton<IServiceConfigGenerator, WinSwXmlGenerator>();
        services.AddSingleton<IHealthChecker, HttpClientHealthChecker>();
        services.AddSingleton<IWindowsServiceMonitor, WindowsServiceMonitor>();
        services.AddSingleton<IServiceManager, WinSwServiceManager>();
    }

    static RootCommand BuildCommandLine(IServiceProvider sp)
    {
        var rootCommand = new RootCommand("Windows Service Manager CLI");

        var apiCommand = new Command("api", "API commands");
        var serveCommand = new Command("serve", "Start the local API server");
        var urlsOption = new Option<string?>("--urls", () => null, "Listen URLs (e.g. http://localhost:9011)");
        var manifestDirOption = new Option<string?>("--manifest-dir", () => null, "Manifest directory path");
        serveCommand.AddOption(urlsOption);
        serveCommand.AddOption(manifestDirOption);
        serveCommand.SetHandler(async (string? urls, string? manifestDir) =>
        {
            var builder = WebApplication.CreateBuilder();

            if (urls is not null)
                builder.Configuration["Winsvc:Api:Urls"] = urls;
            if (manifestDir is not null)
                builder.Configuration["Winsvc:ManifestDirectory"] = manifestDir;

            builder.AddWinsvcApi();

            var app = builder.Build();
            app.MapWinsvcApiEndpoints();

            await app.RunAsync();
        }, urlsOption, manifestDirOption);
        apiCommand.AddCommand(serveCommand);

        var listCommand = new Command("list", "List services");
        var listWindowsCommand = new Command("windows", "List all Windows services");
        listWindowsCommand.SetHandler(async () =>
        {
            var monitor = sp.GetRequiredService<IWindowsServiceMonitor>();
            var services = await monitor.GetAllServicesAsync();
            foreach (var svc in services)
            {
                Console.WriteLine($"{svc.Id} - {svc.DisplayName} ({svc.State}) [{svc.StartMode}]");
            }
        });

        var listManagedCommand = new Command("managed", "List managed services based on manifests");
        listManagedCommand.SetHandler(async () =>
        {
            var manifestDirectory = ManifestPathResolver.ResolveDirectory(
                configuredPath: null,
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory());
            if (!Directory.Exists(manifestDirectory))
            {
                Console.WriteLine($"No manifests directory found: {manifestDirectory}");
                return;
            }

            var reader = sp.GetRequiredService<IManifestReader>();
            var monitor = sp.GetRequiredService<IWindowsServiceMonitor>();
            
            foreach (var file in ManifestPathResolver.EnumerateManifestPaths(manifestDirectory))
            {
                try {
                    var manifest = await reader.ReadAsync(file);
                    var status = await monitor.GetServiceAsync(manifest.Id);
                    var stateStr = status != null ? status.State.ToString() : "NotInstalled";
                    Console.WriteLine($"{manifest.Id} - {manifest.DisplayName} ({stateStr})");
                } catch {
                    Console.WriteLine($"failed to read {file}");
                }
            }
        });

        listCommand.AddCommand(listWindowsCommand);
        listCommand.AddCommand(listManagedCommand);

        var idArg = new Argument<string>("id", "Service ID");

        var renderCommand = new Command("render", "Render WinSW XML from manifest");
        renderCommand.AddArgument(idArg);
        renderCommand.SetHandler(async (string id) =>
        {
            var manifest = await LoadManifest(sp, id);
            if (manifest == null) return;

            var xml = sp.GetRequiredService<IServiceConfigGenerator>().Generate(manifest);
            Console.WriteLine(xml);
        }, idArg);

        var installCommand = new Command("install", "Install the service using WinSW");
        installCommand.AddArgument(idArg);
        installCommand.SetHandler(async (string id) =>
        {
            var manifest = await LoadManifest(sp, id);
            if (manifest == null) return;

            var monitor = sp.GetRequiredService<IWindowsServiceMonitor>();
            var existing = await monitor.GetServiceAsync(manifest.Id);
            if (existing is not null)
            {
                Console.Error.WriteLine($"Service '{id}' is already installed. Use 'winsvc reinstall {id}' to replace it.");
                return;
            }

            Console.WriteLine($"Installing {id}...");
            var xml = sp.GetRequiredService<IServiceConfigGenerator>().Generate(manifest);
            await sp.GetRequiredService<IServiceManager>().InstallAsync(manifest, xml);
            Console.WriteLine("Done.");
        }, idArg);

        var reinstallCommand = new Command("reinstall", "Reinstall the service using WinSW");
        reinstallCommand.AddArgument(idArg);
        reinstallCommand.SetHandler(async (string id) =>
        {
            var manifest = await LoadManifest(sp, id);
            if (manifest == null) return;

            var serviceManager = sp.GetRequiredService<IServiceManager>();
            var monitor = sp.GetRequiredService<IWindowsServiceMonitor>();
            var existing = await monitor.GetServiceAsync(manifest.Id);

            if (existing is not null)
            {
                await UninstallAndWaitAsync(manifest, serviceManager, monitor);
            }

            Console.WriteLine($"Installing {id}...");
            var xml = sp.GetRequiredService<IServiceConfigGenerator>().Generate(manifest);
            await serviceManager.InstallAsync(manifest, xml);
            Console.WriteLine("Done.");
        }, idArg);

        var uninstallCommand = new Command("uninstall", "Uninstall the service using WinSW");
        uninstallCommand.AddArgument(idArg);
        uninstallCommand.SetHandler(async (string id) =>
        {
            var manifest = await LoadManifest(sp, id);
            if (manifest == null) return;

            var serviceManager = sp.GetRequiredService<IServiceManager>();
            var monitor = sp.GetRequiredService<IWindowsServiceMonitor>();
            var existing = await monitor.GetServiceAsync(manifest.Id);
            if (existing is null)
            {
                Console.WriteLine($"Service '{id}' is not installed.");
                return;
            }

            await UninstallAndWaitAsync(manifest, serviceManager, monitor);
            Console.WriteLine("Done.");
        }, idArg);

        var startCommand = new Command("start", "Start the service");
        startCommand.AddArgument(idArg);
        startCommand.SetHandler(async (string id) =>
        {
            var manifest = await LoadManifest(sp, id);
            if (manifest == null) return;
            Console.WriteLine($"Starting {id}...");
            await sp.GetRequiredService<IServiceManager>().StartAsync(manifest);
            Console.WriteLine("Done.");
        }, idArg);

        var stopCommand = new Command("stop", "Stop the service");
        stopCommand.AddArgument(idArg);
        stopCommand.SetHandler(async (string id) =>
        {
            var manifest = await LoadManifest(sp, id);
            if (manifest == null) return;
            Console.WriteLine($"Stopping {id}...");
            await sp.GetRequiredService<IServiceManager>().StopAsync(manifest);
            Console.WriteLine("Done.");
        }, idArg);

        var restartCommand = new Command("restart", "Restart the service");
        restartCommand.AddArgument(idArg);
        restartCommand.SetHandler(async (string id) =>
        {
            var manifest = await LoadManifest(sp, id);
            if (manifest == null) return;
            Console.WriteLine($"Restarting {id}...");
            await sp.GetRequiredService<IServiceManager>().RestartAsync(manifest);
            Console.WriteLine("Done.");
        }, idArg);

        var statusCommand = new Command("status", "Check OS status of a service");
        statusCommand.AddArgument(idArg);
        statusCommand.SetHandler(async (string id) =>
        {
            var monitor = sp.GetRequiredService<IWindowsServiceMonitor>();
            var status = await monitor.GetServiceAsync(id);
            if (status == null) Console.WriteLine($"Service '{id}' not found in OS.");
            else Console.WriteLine($"{status.Id}: {status.State}");
        }, idArg);

        var healthCommand = new Command("health", "Check HTTP health of a managed service");
        healthCommand.AddArgument(idArg);
        healthCommand.SetHandler(async (string id) =>
        {
            var manifest = await LoadManifest(sp, id);
            if (manifest == null) return;

            var state = await sp.GetRequiredService<IHealthChecker>().CheckAsync(manifest.Health);
            Console.WriteLine($"{id} health: {state}");
        }, idArg);

        var showCommand = new Command("show", "Show detailed loaded manifest config");
        showCommand.AddArgument(idArg);
        showCommand.SetHandler(async (string id) =>
        {
            var manifestDirectory = ManifestPathResolver.ResolveDirectory(
                configuredPath: null,
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory());
            var manifestPath = ManifestPathResolver.FindManifestPath(manifestDirectory, id);
            if (manifestPath is not null)
            {
                var content = await File.ReadAllTextAsync(manifestPath);
                Console.WriteLine(content);
            }
        }, idArg);


        rootCommand.AddCommand(apiCommand);
        rootCommand.AddCommand(listCommand);
        rootCommand.AddCommand(renderCommand);
        rootCommand.AddCommand(installCommand);
        rootCommand.AddCommand(reinstallCommand);
        rootCommand.AddCommand(uninstallCommand);
        rootCommand.AddCommand(startCommand);
        rootCommand.AddCommand(stopCommand);
        rootCommand.AddCommand(restartCommand);
        rootCommand.AddCommand(statusCommand);
        rootCommand.AddCommand(healthCommand);
        rootCommand.AddCommand(showCommand);

        return rootCommand;
    }

    static async Task UninstallAndWaitAsync(
        ServiceManifest manifest,
        IServiceManager serviceManager,
        IWindowsServiceMonitor monitor)
    {
        await StopIfNeededAsync(manifest, serviceManager, monitor);

        Console.WriteLine($"Uninstalling {manifest.Id}...");
        await serviceManager.UninstallAsync(manifest);

        if (!await WaitUntilServiceRemovedAsync(manifest.Id, monitor))
        {
            throw new InvalidOperationException(
                $"Service '{manifest.Id}' was marked for deletion but is still visible to Windows. " +
                "Close Service Manager, stop any running winsvc API process, or reboot, then try again.");
        }
    }

    static async Task StopIfNeededAsync(
        ServiceManifest manifest,
        IServiceManager serviceManager,
        IWindowsServiceMonitor monitor)
    {
        var current = await monitor.GetServiceAsync(manifest.Id);
        if (current is null || current.State == ServiceState.Stopped)
        {
            return;
        }

        Console.WriteLine($"Stopping {manifest.Id}...");
        await serviceManager.StopAsync(manifest);
    }

    static async Task<bool> WaitUntilServiceRemovedAsync(string id, IWindowsServiceMonitor monitor)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (await monitor.GetServiceAsync(id) is null)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        return false;
    }

    static async Task<ServiceManifest?> LoadManifest(IServiceProvider sp, string id)
    {
        var manifestDirectory = ManifestPathResolver.ResolveDirectory(
            configuredPath: null,
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory());
        var manifestPath = ManifestPathResolver.FindManifestPath(manifestDirectory, id);
        if (manifestPath is null)
        {
            Console.Error.WriteLine($"Manifest not found for service-id '{id}' in {manifestDirectory}");
            return null;
        }

        var reader = sp.GetRequiredService<IManifestReader>();
        return await reader.ReadAsync(manifestPath);
    }
}
