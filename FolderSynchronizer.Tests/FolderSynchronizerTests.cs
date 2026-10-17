using System.Security.Cryptography;
using FolderSynchronizer.Abstraction;
using FolderSynchronizer.Comparers;
using FolderSynchronizer.Enums;
using FolderSynchronizer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FolderSynchronizer.Tests;

public class FolderSynchronizerTests : IDisposable
{
    private readonly string _sourcePath;
    private readonly string _replicaPath;
    private readonly ILogger<FolderSynchronizer> _logger;
    private readonly IFileComparer _fileComparer;
    public FolderSynchronizerTests()
    {
        _sourcePath = Path.Combine(Path.GetTempPath(), "Tests_Source");
        _replicaPath = Path.Combine(Path.GetTempPath(), "Tests_Replica");
        Directory.CreateDirectory(_sourcePath);
        Directory.CreateDirectory(_replicaPath);

        _logger = NullLogger<FolderSynchronizer>.Instance;
        _fileComparer = new HashComparer(MD5.HashDataAsync);
    }
    public void Dispose()
    {
        Directory.Delete(_sourcePath, true);
        Directory.Delete(_replicaPath, true);
    }
    [Fact]
    public async Task StartAsync_CopiesNewFilesAndDirectoriesToReplica()
    {
        string subDir = Path.Combine(_sourcePath, "SubFolder");
        Directory.CreateDirectory(subDir);
        string sourceFile = Path.Combine(subDir, "test.txt");
        await File.WriteAllTextAsync(sourceFile, "Hello World");

        var options = CreateOptions();
        using var cts = new CancellationTokenSource(millisecondsDelay:150);

        await options.StartAsync(cts.Token);

        string replicaFile = Path.Combine(_replicaPath, "SubFolder", "test.txt");
        Assert.True(File.Exists(replicaFile));
        Assert.Equal("Hello World", await File.ReadAllTextAsync(replicaFile));
    }

    [Fact]
    public async Task StartAsync_UpdatesModifiedFileInReplica()
    {
        string sourceFile = Path.Combine(_sourcePath, "file.txt");
        string replicaFile = Path.Combine(_replicaPath, "file.txt");

        await File.WriteAllTextAsync(replicaFile, "Old Content");
        await File.WriteAllTextAsync(sourceFile, "New Content");

        var options = CreateOptions();
        using var cts = new CancellationTokenSource(millisecondsDelay: 150);

        await options.StartAsync(cts.Token);

        Assert.Equal("New Content", await File.ReadAllTextAsync(replicaFile));
    }
    [Fact]
    public async Task StartAsync_DeletesExtraFilesAndDirectoriesFromReplica()
    {
        string extraDir = Path.Combine(_replicaPath, "ExtraDir");
        Directory.CreateDirectory(extraDir);
        string extraFile = Path.Combine(extraDir, "extra.txt");
        await File.WriteAllTextAsync(extraFile, "Should be deleted");

        var options = CreateOptions();
        using var cts = new CancellationTokenSource(millisecondsDelay: 150);

        await options.StartAsync(cts.Token);

        Assert.False(File.Exists(extraFile));
        Assert.False(Directory.Exists(extraDir));
    }
    [Fact]
    public async Task StartAsync_CreatesReplicaDirectory_WhenItDoesNotExist()
    {
        Directory.Delete(_replicaPath);
        await File.WriteAllTextAsync(Path.Combine(_sourcePath, "test.txt"), "data");

        var options = CreateOptions();
        using var cts = new CancellationTokenSource(millisecondsDelay: 150);

        await options.StartAsync(cts.Token);

        Assert.True(Directory.Exists(_replicaPath));
        Assert.True(File.Exists(Path.Combine(_replicaPath, "test.txt")));
    }
    private FolderSynchronizer CreateOptions()
    {
        var options = new CommandLineOptions
        {
            SourcePath = _sourcePath,
            ReplicaPath = _replicaPath,
            LogFilePath = Path.Combine(Path.GetTempPath(), "log.txt"),
            SyncInterval = 5000,
            Comparator = EComparator.MD5,
            MaxRetryAttempts = 3,
            RetryDelayMilliseconds = 50,
            MaxParallelThreads = 2
        };

        return new FolderSynchronizer(options, _logger, _fileComparer);
    }
}
