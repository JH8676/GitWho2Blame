using System.Reflection;
using GitWho2Blame.GitHub.Options;
using GitWho2Blame.MCP.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GitWho2Blame.GitHub.Startup;

public static class ServiceExtensions
{
    public static IServiceCollection AddGitHubServices(this IServiceCollection services, IConfigurationManager configuration)
    {
        configuration.AddUserSecrets(Assembly.GetCallingAssembly(), optional: false);
        
        services.Configure<GitHubOptions>(
            configuration.GetSection(GitHubOptions.GitHub)
        );
        
        services.AddSingleton<IGitContextProvider, GitHubContextProvider>();
        
        return services;
    }
}