using System.Runtime.InteropServices;
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
            .AddSerilog(dispose: true)
            .AddConsole(consoleLogOptions =>
            {
                // Configure all logs to go to stderr
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            });;
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
}