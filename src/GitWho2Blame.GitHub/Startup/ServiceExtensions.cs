using GitWho2Blame.GitHub.Options;
using GitWho2Blame.MCP.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Octokit;

namespace GitWho2Blame.GitHub.Startup;

public static class ServiceExtensions
{
    public static IServiceCollection AddGitHubServices(this IServiceCollection services, IConfigurationManager configuration)
    {
        services.Configure<GitHubOptions>(options =>
        {
            options.Token = Environment.GetEnvironmentVariable("TOKEN")
                            ?? throw new ArgumentNullException("TOKEN environment variable is required for GitHub authentication");
        });

        services.AddScoped<IGitHubClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
            return new GitHubClient(new ProductHeaderValue(nameof(GitWho2Blame).ToLower()))
            {
                Credentials = new Credentials(options.Token)
            };
        });
        
        services.AddScoped<IGitContextProvider, GitHubContextProvider>();
        
        return services;
    }
}