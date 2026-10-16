using FolderSynchronizer.Comparers;
using FolderSynchronizer.Enumerators;
using Microsoft.Extensions.Logging;
namespace FolderSynchronizer;

public class Synchronizer(Options options, ILogger<Synchronizer> logger, IFileComparer fileComparer)
{
    private readonly int _maxRetryAttempts = options.MaxRetryAttemps;
    private readonly int _retryDelayMilliseconds = options.RetryDelayMilliseconds;
    private readonly string _sourcePath = options.SourcePath;
    private readonly string _replicaPath = options.ReplicaPath;
    private readonly ILogger<Synchronizer> _logger = logger;
    private readonly int _syncInterval = options.SyncInterval;
    private readonly EComparator _comparingMethod = options.Comparator;
    private readonly IFileComparer _fileComparer = fileComparer;
    public async Task StartAsync(CancellationToken cancellationToken)
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
                await Task.WhenAll(syncTask, cleanTask);
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
            }

        }
    }
    private async Task SynchronizeFilesAndDirectoriesAsync(DirectoryInfo sourceDirectory, CancellationToken cancellationToken)
    {
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
            }
        }
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 8,
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
                await ExecuteWithRetryAsync(async () =>
                {
                    shouldCopy = !await _fileComparer.IsFilesEqual(file, replicaFile, ct);
                }, ct);
                if (shouldCopy)
                {
                    await CopyFileAsync(file.FullName, replicaFilePath, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError($"Error during file {file.FullName} synchronization: {ex.Message}");
            }
        });


    }

    private async Task CopyFileAsync(string fullName, string replicaFilePath, CancellationToken cancellationToken)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            using FileStream sourceStream = new(fullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream targetStream = new(replicaFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(targetStream, cancellationToken);
            _logger.LogInformation($"Copied {fullName} to {replicaFilePath}");
        }, cancellationToken);
    }

    private async Task ExecuteWithRetryAsync(Func<Task> action, CancellationToken cancellationToken) //for FS errors e.g. file is open.
    {
        for (int attempt = 0; attempt < _maxRetryAttempts; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (IOException ioEx) when (attempt < _maxRetryAttempts - 1)
            {
                _logger.LogWarning($"Failed (attempt {attempt + 1}): {ioEx.Message}. Retrying...");
                await Task.Delay(_retryDelayMilliseconds, cancellationToken);
            }
            catch (IOException ioEx)
            {
                _logger.LogError($"Failed on last attempt: {ioEx.Message}");
                throw;
            }
        }
    }
    private async Task CleanReplicaAsync(DirectoryInfo replicaDirectory, CancellationToken cancellationToken)
    {
        var parallelOptions = new ParallelOptions
        {

            MaxDegreeOfParallelism = 8,
            CancellationToken = cancellationToken
        };
        await Parallel.ForEachAsync(replicaDirectory.EnumerateFiles("*", SearchOption.AllDirectories), parallelOptions, async (file, ct) =>
        {
            try
            {
                string relativePath = Path.GetRelativePath(_replicaPath, file.FullName);
                string sourceFilePath = Path.Combine(_sourcePath, relativePath);
                if (!File.Exists(sourceFilePath))
                {
                    if (File.Exists(file.FullName))
                    {
                        await ExecuteWithRetryAsync(() =>
                        {
                            file.Delete();
                            return Task.CompletedTask;
                        }, ct);
                        _logger.LogInformation($"Deleted {file.FullName} from replica directory.");
                    }

                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError($"Error during cleaning {file.FullName}: {ex.Message}");
            }
        });
        IOrderedEnumerable<DirectoryInfo> replicaDirs = replicaDirectory.GetDirectories("*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.FullName.Length);
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
            }
        }
    }


}