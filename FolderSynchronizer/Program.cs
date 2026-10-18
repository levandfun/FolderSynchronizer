using System.Security.Cryptography;
using FolderSynchronizer.Abstraction;
using FolderSynchronizer.Comparers;
using FolderSynchronizer.Enums;
using FolderSynchronizer.Helpers;
using FolderSynchronizer.Models;
using Microsoft.Extensions.Logging;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;

namespace FolderSynchronizer;

public class Program
{
    static async Task<int> Main(string[] args)
    {
        CommandLineOptions? options = CommandLineParser.ParseArguments(args);
        if (options is null)
        {
            return -1;
        }
        var loggerConfig = new LoggingConfiguration();
        var fileTarget = new FileTarget("file") { FileName = options.LogFilePath };
        var consoleTarget = new ConsoleTarget("console");
        loggerConfig.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, consoleTarget);
        loggerConfig.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, fileTarget);
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddNLog(loggerConfig);
        });

        try
        {
            using CancellationTokenSource cts = new();

            ILogger<FolderSynchronizer> logger = loggerFactory.CreateLogger<FolderSynchronizer>();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                logger.LogInformation("Cancellation requested. Stopping synchronization...");
                cts.Cancel();
            };

            IFileComparer fileComparer = options.Comparator switch
            {
                EComparator.MD5 => new HashComparer(MD5.HashDataAsync),
                EComparator.SHA256 => new HashComparer(SHA256.HashDataAsync),
                EComparator.NONE => new FastComparer(),
                EComparator.BINARY => new BinaryComparer(),
                _ => throw new InvalidOperationException("No comparator was found")

            };

            FolderSynchronizer synchronizer = new (options, logger, fileComparer);

            await synchronizer.StartAsync(cts.Token);

            logger.LogInformation("Synchronization process completed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical error occurred, unable to proceed: {ex.Message}");
            return 1;
        }
        finally
        {
            NLog.LogManager.Shutdown();
        }
    }
}
