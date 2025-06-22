using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace GitWho2Blame.Startup;

public static class HostApplicationExtensions
{
    public static async Task RunAppAsync(this IHostApplicationBuilder builder)
    {
        switch (builder)
        {
            case WebApplicationBuilder webApplicationBuilder:
                var webApp = webApplicationBuilder.Build();
                webApp.MapMcp("/mcp");
                await webApp.RunAsync();
                break;
        
            case HostApplicationBuilder hostApplicationBuilder:
                var hostApp = hostApplicationBuilder.Build();
                await hostApp.RunAsync();
                break;
        
            default:
                break;
        }
    }
}