using GitWho2Blame.MCP.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace GitWho2Blame.GitHub.Startup;

public static class ServiceExtensions
{
    public static IServiceCollection AddGitHubServices(this IServiceCollection services)
    {
        services
            .AddSingleton<IGitContextProvider, GitHubContextProvider>();

        return services;
    }
}