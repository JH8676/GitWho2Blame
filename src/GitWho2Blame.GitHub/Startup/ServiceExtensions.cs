using System.Reflection;
using GitWho2Blame.GitHub.Options;
using GitWho2Blame.MCP.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GitWho2Blame.GitHub.Startup;

public static class ServiceExtensions
{
    public static IServiceCollection AddGitHubServices(this IServiceCollection services, IConfigurationManager configuration)
    {
        configuration.AddUserSecrets(Assembly.GetCallingAssembly(), optional: false);
        
        // services.Configure<GitHubOptions>(
        //     configuration.GetSection(GitHubOptions.GitHub)
        // );

        services.Configure<GitHubOptions>(options =>
        {
            options.Token = Environment.GetEnvironmentVariable("TOKEN")
                            ?? throw new ArgumentNullException("TOKEN environment variable is required for GitHub authentication");
        });
        
        services.AddSingleton<IGitContextProvider, GitHubContextProvider>();
        
        return services;
    }
}