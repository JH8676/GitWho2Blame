using GitWho2Blame.MCP.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace GitWho2Blame.Git.Startup;

public static class ServiceExtensions
{
    public static IServiceCollection AddGitServices(this IServiceCollection services)
    {
        services
            .AddSingleton<IGitService, GitService>(_ => new GitService("path/to/git/repo route"));

        return services;
    }
}