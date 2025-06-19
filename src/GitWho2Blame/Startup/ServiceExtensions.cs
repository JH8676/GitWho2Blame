using System.Runtime.InteropServices;
using GitWho2Blame.Azure.Startup;
using GitWho2Blame.GitHub.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GitWho2Blame.Startup;

public static class ServiceExtensions
{
    public static ILoggingBuilder ConfigureLogging(this ILoggingBuilder logging)
    {
        const string logDirectoryName = "gitwho2blame";
        const string logFileName = $"{logDirectoryName}.log";
        
        var userLogPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                logDirectoryName, logFileName)
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) 
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Logs", logDirectoryName, logFileName)
                : throw new PlatformNotSupportedException("Unsupported OS for logging");
        
        Directory.CreateDirectory(Path.GetDirectoryName(userLogPath)!);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(userLogPath)
            .CreateLogger();
        
        return logging
            .ClearProviders()
            .AddSerilog(dispose: true);
    }
    
    public static void AddGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled domain exception");
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Log.Fatal(e.Exception, "Unobserved task exception");
            Log.CloseAndFlush();
        };
    }

    public static IServiceCollection HandleArgs(this IServiceCollection services, IConfigurationManager configuration, string[] args)
    {
        switch (args)
        {
            case ["--git-context-provider", var provider]:
            switch (provider)
            {
                case "github":
                {
                    services.AddGitHubServices(configuration);
                    break;
                }
                case "azure":
                {
                    services.AddAzureGitServices(configuration);
                    break;
                }
                default: throw new ArgumentException($"Unknown git context provider: {provider}");
            }

            break;
    
            default: throw new ArgumentException("Expected --git-context-provider argument");
        };
        
        return services;
    }
}