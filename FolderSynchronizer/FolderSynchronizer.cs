using FolderSynchronizer.Abstraction;
using FolderSynchronizer.Helpers;
using FolderSynchronizer.Models;
using Microsoft.Extensions.Logging;

namespace FolderSynchronizer;

public class FolderSynchronizer(CommandLineOptions options, ILogger<FolderSynchronizer> logger, IFileComparer fileComparer)
{
    private readonly RetryExecutor _retryExecutor = new RetryExecutor(logger, options.MaxRetryAttempts, options.RetryDelayMilliseconds);
    private readonly int _maxParralelThreads = options.MaxParallelThreads;
    private readonly string _sourcePath = options.SourcePath;
    private readonly string _replicaPath = options.ReplicaPath;
    private readonly ILogger<FolderSynchronizer> _logger = logger;
    private readonly int _syncInterval = options.SyncInterval;
    private readonly IFileComparer _fileComparer = fileComparer;
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting synchronization");
                DateTime start = DateTime.Now;
                DirectoryInfo sourceDirectory = new(_sourcePath);
                DirectoryInfo replicaDirectory = new(_replicaPath);
                if (!sourceDirectory.Exists)
                {
                    _logger.LogError($"Source directory '{_sourcePath}' does not exist.");
                    return;
                }
                if (!replicaDirectory.Exists)
                {
                    _logger.LogInformation($"Creating replica directory: {_replicaPath}");
                    replicaDirectory.Create();
                }
                Task syncTask = SynchronizeFilesAndDirectoriesAsync(sourceDirectory, cancellationToken);
                Task cleanTask = CleanReplicaAsync(replicaDirectory, cancellationToken);
                await Task.WhenAll(syncTask, cleanTask); //never will try to delete and copy same files, so race is impossible
                _logger.LogInformation($"Finished synchronization, {(DateTime.Now - start).TotalMilliseconds} ms elapsed.");
                await Task.Delay(_syncInterval, cancellationToken);

            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during synchronization: {ex.Message}");
                throw;
            }

        }
    }
    private async Task SynchronizeFilesAndDirectoriesAsync(DirectoryInfo sourceDirectory, CancellationToken cancellationToken)
    {
        //EnumerateDirectories returns directories in top-down order, ensuring parents are created before children
        foreach (DirectoryInfo dir in sourceDirectory.EnumerateDirectories("*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string relativePath = Path.GetRelativePath(_sourcePath, dir.FullName);
                string replicaSubDirPath = Path.Combine(_replicaPath, relativePath);
                DirectoryInfo replicaSubDir = new(replicaSubDirPath);
                if (!replicaSubDir.Exists)
                {
                    replicaSubDir.Create();
                    _logger.LogInformation($"Created directory: {replicaSubDir}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during directories synchronization: {ex.Message}");
                throw;
            }
        }
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxParralelThreads,
            CancellationToken = cancellationToken
        };
        await Parallel.ForEachAsync(sourceDirectory.EnumerateFiles("*", SearchOption.AllDirectories), parallelOptions, async (file, ct) =>
        {
            try
            {
                bool shouldCopy = false;
                string relativePath = Path.GetRelativePath(_sourcePath, file.FullName);
                string replicaFilePath = Path.Combine(_replicaPath, relativePath);
                FileInfo replicaFile = new(replicaFilePath);
                await _retryExecutor.ExecuteWithRetryAsync(async () =>
                {
                    shouldCopy = !await _fileComparer.AreFilesEqualAsync(file, replicaFile, ct);
                }, ct);
                if (shouldCopy)
                {
                    await _retryExecutor.ExecuteWithRetryAsync(() => CopyFileAsync(file.FullName, replicaFilePath, ct), ct);
                }

            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError($"Error during file {file.FullName} synchronization: {ex.Message}");
                throw;
            }
        });

    }

    private async Task CopyFileAsync(string fullName, string replicaFilePath, CancellationToken cancellationToken)
    {
        using FileStream sourceStream = new(fullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream targetStream = new(replicaFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await sourceStream.CopyToAsync(targetStream, cancellationToken);
        _logger.LogInformation($"Copied {fullName} to {replicaFilePath}");
    }


    private async Task CleanReplicaAsync(DirectoryInfo replicaDirectory, CancellationToken cancellationToken)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxParralelThreads,
            CancellationToken = cancellationToken
        };
        await Parallel.ForEachAsync(replicaDirectory.EnumerateFiles("*", SearchOption.AllDirectories), parallelOptions, async (replicaFile, ct) =>
        {
            try
            {
                string relativePath = Path.GetRelativePath(_replicaPath, replicaFile.FullName);
                string sourceFilePath = Path.Combine(_sourcePath, relativePath);
                if (!File.Exists(sourceFilePath))
                {
                    if (replicaFile.Exists)
                    {
                        await _retryExecutor.ExecuteWithRetryAsync(replicaFile.Delete, ct);
                        _logger.LogInformation($"Deleted {replicaFile.FullName} from replica directory.");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError($"Error during cleaning {replicaFile.FullName}: {ex.Message}");
                throw;
            }
        });

        IOrderedEnumerable<DirectoryInfo> replicaDirs = replicaDirectory.GetDirectories("*", SearchOption.AllDirectories).OrderByDescending(d => d.FullName.Length);

        foreach (DirectoryInfo dir in replicaDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string relativePath = Path.GetRelativePath(_replicaPath, dir.FullName);
                string sourceDirPath = Path.Combine(_sourcePath, relativePath);

                if (!Directory.Exists(sourceDirPath))
                {
                    dir.Delete(true);
                    _logger.LogInformation($"Deleted {dir.FullName} from replica directory");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during cleaning {dir.FullName}: {ex.Message}");
                throw;
            }
        }
    }
}
