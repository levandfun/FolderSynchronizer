using Microsoft.Extensions.Logging;

namespace FolderSynchronizer.Helpers;

public class RetryExecutor(ILogger? logger = null, int maxRetryAttempts = 5, int retryDelayMilliseconds = 1000)
{
    private readonly ILogger? _logger = logger;
    private readonly int _maxRetryAttempts = maxRetryAttempts;
    private readonly int _retryDelayMilliseconds = retryDelayMilliseconds;
    public async Task ExecuteWithRetryAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < _maxRetryAttempts; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await action();
                return;
            }
            catch (IOException ioEx) when (attempt < _maxRetryAttempts - 1)
            {
                _logger?.LogWarning($"Failed (attempt {attempt + 1}): {ioEx.Message}. Retrying...");
                await Task.Delay(_retryDelayMilliseconds, cancellationToken);
            }
            catch (IOException ioEx)
            {
                _logger?.LogError($"Failed on last attempt: {ioEx.Message}");
                throw;
            }
        }
    }
    public Task ExecuteWithRetryAsync(Action action, CancellationToken cancellationToken = default)
    {
        return ExecuteWithRetryAsync(() => { action.Invoke(); return Task.CompletedTask; }, cancellationToken);
    }
}

