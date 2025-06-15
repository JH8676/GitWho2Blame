using Microsoft.Extensions.DependencyInjection;

namespace GitWho2Blame.MCP.Startup;

public static class ServiceExtensions
{
    public static IServiceCollection AddMcpServerServices(this IServiceCollection services)
    {
        services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();
        
        return services;
    }
}