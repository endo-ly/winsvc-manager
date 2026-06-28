using Microsoft.AspNetCore.Http;
using Winsvc.Contracts;
using Winsvc.Contracts.Api;
using Winsvc.Core;

namespace Winsvc.Hosting;

public static class ApiEndpointExtensions
{
    public static WebApplication MapWinsvcApiEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new ApiInfoResponse("winsvc-manager", "ok")));

        app.MapGet("/services/windows", async (IWindowsServiceMonitor monitor) =>
        {
            var services = await monitor.GetAllServicesAsync();
            var response = services
                .OrderBy(service => service.Id, StringComparer.OrdinalIgnoreCase)
                .Select(MapWindowsServiceResponse);

            return Results.Ok(response);
        });

        app.MapGet("/services/managed", async (
            IManifestProvider manifestProvider,
            IWindowsServiceMonitor monitor,
            CancellationToken cancellationToken) =>
        {
            var manifests = await manifestProvider.GetManifestsAsync(cancellationToken);
            var response = new List<ManagedServiceResponse>();

            foreach (var manifest in manifests)
            {
                var windowsService = await monitor.GetServiceAsync(manifest.Id);
                response.Add(new ManagedServiceResponse(
                    manifest.Id,
                    manifest.DisplayName,
                    manifest.Description,
                    manifest.Type,
                    MapServiceState(windowsService),
                    windowsService?.StartMode,
                    manifest.Health.Url));
            }

            return Results.Ok(response);
        });

        app.MapGet("/services/{id}", async (
            string id,
            IManifestProvider manifestProvider,
            IWindowsServiceMonitor monitor,
            CancellationToken cancellationToken) =>
        {
            var manifest = await manifestProvider.GetManifestAsync(id, cancellationToken);
            if (manifest is null)
            {
                return Results.NotFound(new ErrorResponse($"Managed service '{id}' was not found."));
            }

            var windowsService = await monitor.GetServiceAsync(manifest.Id);

            return Results.Ok(new ManagedServiceDetailResponse(
                manifest.Id,
                manifest.DisplayName,
                manifest.Description,
                manifest.Type,
                MapServiceState(windowsService),
                windowsService?.StartMode,
                manifest.Health.Url,
                manifest.Service.WrapperDir,
                manifest.Runtime.WorkDir,
                manifest.Exposure.TailscaleServe.Enabled,
                manifest.Exposure.TailscaleServe.HttpsPort,
                manifest.Exposure.TailscaleServe.Target));
        });

        app.MapGet("/services/{id}/health", async (
            string id,
            IManifestProvider manifestProvider,
            IHealthChecker healthChecker,
            CancellationToken cancellationToken) =>
        {
            var manifest = await manifestProvider.GetManifestAsync(id, cancellationToken);
            if (manifest is null)
            {
                return Results.NotFound(new ErrorResponse($"Managed service '{id}' was not found."));
            }

            var health = await healthChecker.CheckAsync(manifest.Health);

            return Results.Ok(new ServiceHealthResponse(
                manifest.Id,
                health.ToString(),
                manifest.Health.Url,
                manifest.Health.TimeoutSec));
        });

        app.MapPost("/services/{id}/start", async (
            string id,
            IManifestProvider manifestProvider,
            IServiceManager serviceManager,
            CancellationToken cancellationToken) =>
        {
            var manifest = await manifestProvider.GetManifestAsync(id, cancellationToken);
            if (manifest is null)
            {
                return Results.NotFound(new ErrorResponse($"Managed service '{id}' was not found."));
            }

            await serviceManager.StartAsync(manifest);
            return Results.Ok(new ServiceActionResponse(manifest.Id, "start", "queued"));
        });

        app.MapPost("/services/{id}/stop", async (
            string id,
            IManifestProvider manifestProvider,
            IServiceManager serviceManager,
            CancellationToken cancellationToken) =>
        {
            var manifest = await manifestProvider.GetManifestAsync(id, cancellationToken);
            if (manifest is null)
            {
                return Results.NotFound(new ErrorResponse($"Managed service '{id}' was not found."));
            }

            await serviceManager.StopAsync(manifest);
            return Results.Ok(new ServiceActionResponse(manifest.Id, "stop", "queued"));
        });

        app.MapPost("/services/{id}/restart", async (
            string id,
            IManifestProvider manifestProvider,
            IServiceManager serviceManager,
            CancellationToken cancellationToken) =>
        {
            var manifest = await manifestProvider.GetManifestAsync(id, cancellationToken);
            if (manifest is null)
            {
                return Results.NotFound(new ErrorResponse($"Managed service '{id}' was not found."));
            }

            await serviceManager.RestartAsync(manifest);
            return Results.Ok(new ServiceActionResponse(manifest.Id, "restart", "queued"));
        });

        return app;
    }

    static string MapServiceState(WindowsServiceInfo? windowsService)
    {
        return (windowsService?.State ?? ServiceState.NotFound).ToString();
    }

    static WindowsServiceResponse MapWindowsServiceResponse(WindowsServiceInfo service)
    {
        return new WindowsServiceResponse(
            service.Id,
            service.DisplayName,
            service.State.ToString(),
            service.StartMode);
    }
}
