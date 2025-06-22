using System.CommandLine;
using GitWho2Blame.Enums;
using GitWho2Blame.MCP.Enums;

namespace GitWho2Blame.Cli;

public static class CliOptions
{
    public static Option<GitContextProvider> GitContextProviderOption { get; } = new(
        name: "--git-context-provider",
        aliases: "-g")
    {
        Description = "The Git context provider to use. Default is 'GitHub'.",
        Required = false,
        DefaultValueFactory = _ => GitContextProvider.GitHub,
        CompletionSources = { Enum.GetNames<GitContextProvider>() },
    };
    
    public static Option<TransportType> TransportTypeOption { get; } = new(
        name: "--transport-type",
        aliases: "-t")
    {
        Description = "The transport type to use. Default is 'stdio'.",
        Required = false,
        DefaultValueFactory = _ => TransportType.Stdio,
        CompletionSources = { Enum.GetNames<TransportType>() },
    };
}