using GitWho2Blame.MCP.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace GitWho2Blame.MCP.Startup;

public static class ServiceExtensions
{
    public static IServiceCollection AddMcpServerServices(this IServiceCollection services, TransportType transportType)
    {
        var builder = services.AddMcpServer();

        switch (transportType)
        {
            case TransportType.Stdio:
                builder.WithStdioServerTransport();
                break;
            case TransportType.Http:
                builder.WithHttpTransport();
                break;
            default:
                throw new ArgumentException($"Unknown transport type: {transportType}");
        }

        builder.WithToolsFromAssembly();

        return services;
    }
}