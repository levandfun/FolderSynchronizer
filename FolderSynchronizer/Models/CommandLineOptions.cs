using CommandLine;
using FolderSynchronizer.Enums;

namespace FolderSynchronizer.Models;

public class CommandLineOptions
{
    [Option('s', "source", Required = true, HelpText = "Source folder path")]
    public required string SourcePath { get; init; }

    [Option('r', "replica", Required = true, HelpText = "Replica folder path")]
    public required string ReplicaPath { get; init; }

    [Option('i', "interval", Default = 10000, HelpText = "Sync interval in miliseconds")]
    public int SyncInterval { get; init; }

    [Option('l', "log", Required = true,  HelpText = "Log file path")]
    public required string LogFilePath { get; init; }
    [Option('c', "comparator", Default = EComparator.MD5, HelpText = "Comparator used to ensure files identity (MD5/SHA256/NONE(Only check size))")]
    public EComparator Comparator { get; init; }
    [Option('m', "maxRetryAttempts", Default = 5, HelpText = "Maximum number of attempts for a file operation that fails with an I/O error (e.g. file is locked)")]
    public int MaxRetryAttempts { get; init; }
    [Option('d', "retryDelay", Default = 1000, HelpText = "Delay between retry attempts, in milliseconds"), ]
    public int RetryDelayMilliseconds { get; init; }
    [Option('p', "maxParallelThreads", Default = 8, HelpText = "Maximum parallel Threads for file operations")]
    public int MaxParallelThreads { get; init; }
}
