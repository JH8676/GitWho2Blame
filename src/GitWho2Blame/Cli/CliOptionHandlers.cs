using GitWho2Blame.Azure.Startup;
using GitWho2Blame.Enums;
using GitWho2Blame.GitHub.Startup;
using GitWho2Blame.MCP.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace GitWho2Blame.Cli;

public static class CliOptionHandlers
{
    public static IHostApplicationBuilder HandleTransportType(TransportType transportType, string[] args)
    {
        return transportType switch
        {
            TransportType.Stdio => Host.CreateApplicationBuilder(args),
            TransportType.Http => WebApplication.CreateBuilder(args),
            _ => throw new ArgumentException($"Unknown transport type: {transportType}")
        };
    }

    public static void HandleGitContextProvider(GitContextProvider gitContextProvider, IHostApplicationBuilder builder)
    {
        switch (gitContextProvider)
        {
            case GitContextProvider.GitHub:
                builder.Services.AddGitHubServices(builder.Configuration);
                break;
            case GitContextProvider.Azure:
                builder.Services.AddAzureGitServices(builder.Configuration);
                break;
            default:
                throw new ArgumentException($"Unknown git context provider: {gitContextProvider}");
        }
    }
}