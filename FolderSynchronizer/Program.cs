using FolderSynchronizer.Comparers;
using FolderSynchronizer.Enumerators;
using Microsoft.Extensions.Logging;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using System.Security.Cryptography;

namespace FolderSynchronizer;

public class Program
{
    static async Task Main(string[] args)
    {
        Options? options = Processor.ParseArguments(args);
        if (options is null)
        {
            return;
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








            ILogger<Synchronizer> logger = loggerFactory.CreateLogger<Synchronizer>();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                logger.LogInformation("Cancellation requested. Stopping synchronization...");
                cts.Cancel();
            };


            IFileComparer fileComparer = options.Comparator switch
            {
                EComparator.MD5 => fileComparer = new HashComparer(MD5.HashDataAsync),
                EComparator.SHA256 => fileComparer = new HashComparer(SHA256.HashDataAsync),
                EComparator.NONE => fileComparer = new FastComparer(),
                _ => throw new InvalidOperationException("No comparator was found")

            };

            Synchronizer synchronizer = new(
                options, logger, fileComparer);

            await synchronizer.StartAsync(cts.Token);

            logger.LogInformation("Synchronization process completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical error occurred, unable to proceed: {ex.Message}");
            Environment.Exit(1);
        }
        finally
        {
            NLog.LogManager.Shutdown();
        }
    }
}