using System.Runtime.Versioning;

namespace Winsvc.Hosting;

[SupportedOSPlatform("windows")]
public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddWinsvcApi(this WebApplicationBuilder builder)
    {
        var apiUrls = builder.Configuration["Winsvc:Api:Urls"];
        if (!string.IsNullOrWhiteSpace(apiUrls))
        {
            builder.WebHost.UseUrls(apiUrls);
        }

        builder.Services.AddWinsvcServices(builder.Configuration, builder.Environment.ContentRootPath);
        return builder;
    }
}
