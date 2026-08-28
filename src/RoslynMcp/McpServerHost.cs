using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace RoslynMcp;

[ExcludeFromCodeCoverage(Justification = "The stdio host is verified by a black-box MCP protocol test.")]
public static class McpServerHost
{
    public static async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var informationalVersion = typeof(McpServerHost).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        var version = informationalVersion.Split('+', 2)[0];
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "roslyn-mcp-.log");
#if DEBUG
        const LogEventLevel minimumLevel = LogEventLevel.Debug;
#else
        const LogEventLevel minimumLevel = LogEventLevel.Information;
#endif
        var logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            builder.Logging.ClearProviders();
            builder.Services.AddSerilog(logger, dispose: false);
            builder.Services
                .AddMcpServer(options =>
                {
                    options.ServerInfo = new()
                    {
                        Name = "RoslynMcp",
                        Version = version
                    };
                })
                .WithStdioServerTransport()
                .WithTools<RoslynTools>();

            using var host = builder.Build();
            logger.ForContext("SourceContext", nameof(McpServerHost))
                .Information("RoslynMcp {Version} starting. Log path: {LogPath}", version, logPath);
            await host.RunAsync(cancellationToken);
        }
        finally
        {
            logger.Dispose();
        }
    }
}
