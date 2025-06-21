using Microsoft.Extensions.Hosting;
using GitWho2Blame.Git.Startup;
using GitWho2Blame.MCP.Startup;
using GitWho2Blame.Startup;
using Microsoft.AspNetCore.Builder;
using Serilog;

using ServiceExtensions = GitWho2Blame.Startup.ServiceExtensions;

// var builder = Host.CreateApplicationBuilder(args);
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ConfigureLogging();

ServiceExtensions.AddGlobalExceptionHandlers();

builder.Services
    .HandleArgs(builder.Configuration, args)
    .AddGitServices()
    .AddMcpServerServices();

Log.Information("GitWho2Blame MCP server starting...");

var app = builder.Build();

app.MapMcp("/mcp");

await app.RunAsync();

// await builder.Build().RunAsync();