using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using GitWho2Blame.Git.Startup;
using GitWho2Blame.GitHub.Startup;
using GitWho2Blame.MCP.Abstractions;
using GitWho2Blame.MCP.Startup;

var builder = Host.CreateApplicationBuilder(args);
switch (args)
{
    case ["--git-context-provider", var provider]:
        switch (provider)
        {
            case "github":
            {
                builder.Services.AddGitHubServices();
                break;
            }
            default: throw new ArgumentException($"Unknown git context provider: {provider}");
        }

        break;
    
    default: throw new ArgumentException("Expected --git-context-provider argument");
};

builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddGitServices()
    .AddMcpServerServices();

await builder.Build().RunAsync();