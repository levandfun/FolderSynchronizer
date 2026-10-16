using CommandLine;
using FolderSynchronizer.Enumerators;

namespace FolderSynchronizer;


public class Options
{
    [Option('s', "source", Required = true, HelpText = "Source folder path")]
    //The option is Required, so never null at runtime.
    public string SourcePath { get; init; } = null!;

    [Option('r', "replica", Required = true, HelpText = "Replica folder path")]
    public string ReplicaPath { get; init; } = null!;

    [Option('i', "interval", Default = 10000, HelpText = "Sync interval in miliseconds")]
    public int SyncInterval { get; init; }

    [Option('l', "log", Required = true, HelpText = "Log file path")]
    public string LogFilePath { get; init; } = null!;
    [Option('c', "comparator", Default = "MD5", HelpText = "Comparator used to ensure files identity (MD5/SHA256/NONE(Only check size))")]
    public EComparator Comparator { get; init; } = EComparator.MD5;
    [Option('m', "maxRetryAttemps", Default = 5, HelpText = "Maximum number of attempts for a file operation that fails with an I/O error (e.g. file is locked)")]
    public int MaxRetryAttemps { get; init; }
    [Option('d', "retryDelay", Default = 1000, HelpText = "Delay between retry attempts, in milliseconds")]
    public int RetryDelayMilliseconds { get; init; }
}
public static class Processor
{
    public static Options? ParseArguments(string[] args)
    {
        {
            Options? options = null;
            _ = Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts => options = opts);
            return options;
        }
    }
}
