using GitWho2Blame.Azure.Options;
using GitWho2Blame.MCP.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GitWho2Blame.Azure.Startup;

public static class ServiceExtensions
{
    public static IServiceCollection AddAzureGitServices(this IServiceCollection services, IConfigurationManager configuration)
    {
        services.Configure<AzureGitOptions>(options =>
        {
            options.Token = Environment.GetEnvironmentVariable("TOKEN") ?? throw new ArgumentNullException("AZURE_GIT_TOKEN");
            
            var projectId = Environment.GetEnvironmentVariable("AZURE_GIT_PROJECT_ID");
            options.ProjectId = Guid.TryParse(projectId, out var projectIdGuid)
                ? projectIdGuid
                : throw new ArgumentException("Invalid or missing AZURE_GIT_PROJECT_ID environment variable");
            
            var orgUri = Environment.GetEnvironmentVariable("AZURE_GIT_ORG_URI");
            options.OrgUri = Uri.TryCreate(orgUri, UriKind.Absolute, out var orgUriParsed)
                ? orgUriParsed
                : throw new ArgumentException("Invalid or missing AZURE_GIT_ORG_URI environment variable");
        });
        
        services.AddSingleton<IGitContextProvider, AzureGitContextProvider>();
        
        return services;
    }
}