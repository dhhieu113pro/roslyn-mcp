using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RoslynMcp;

[ExcludeFromCodeCoverage(Justification = "The stdio host is verified by a black-box MCP protocol test.")]
public static class McpServerHost
{
    public static async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services
            .AddMcpServer(options =>
            {
                var informationalVersion = typeof(McpServerHost).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                    .InformationalVersion;
                options.ServerInfo = new()
                {
                    Name = "RoslynMcp",
                    Version = informationalVersion.Split('+', 2)[0]
                };
            })
            .WithStdioServerTransport()
            .WithTools<RoslynTools>();

        await builder.Build().RunAsync(cancellationToken);
    }
}
