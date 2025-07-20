using System.CommandLine;
using GitWho2Blame.Cache.Startup;
using GitWho2Blame.Cli;
using GitWho2Blame.Git.Startup;
using GitWho2Blame.MCP.Startup;
using GitWho2Blame.Startup;
using Serilog;

using ServiceExtensions = GitWho2Blame.Startup.ServiceExtensions;

var rootCommand = new RootCommand
{
    CliOptions.GitContextProviderOption,
    CliOptions.TransportTypeOption
};

rootCommand.SetAction(async parseResult =>
{
    var transportType = parseResult.GetValue(CliOptions.TransportTypeOption);
    var builder = CliOptionHandlers.HandleTransportType(transportType, args);

    var gitContextProvider = parseResult.GetValue(CliOptions.GitContextProviderOption);
    CliOptionHandlers.HandleGitContextProvider(gitContextProvider, builder);
    
    builder.Logging.ConfigureLogging();

    ServiceExtensions.AddGlobalExceptionHandlers();

    builder.Services
        .AddCacheServices()
        .AddGitServices()
        .AddMcpServerServices(transportType);

    Log.Information("GitWho2Blame MCP server starting...");
    await builder.RunAppAsync();
});

await rootCommand.Parse(args).InvokeAsync();