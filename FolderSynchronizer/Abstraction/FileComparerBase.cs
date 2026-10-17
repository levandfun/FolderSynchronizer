
namespace FolderSynchronizer.Abstraction;

public abstract class FileComparerBase : IFileComparer
{
    public async Task<bool> AreFilesEqualAsync(FileInfo file1, FileInfo file2, CancellationToken cancellationToken)
    {
        if (!file2.Exists || file1.LastWriteTime > file2.LastWriteTime || file1.Length != file2.Length)
        {
            return false;
        }
        return await AreContentsEqualAsync(file1, file2, cancellationToken);
    }

    protected abstract Task<bool> AreContentsEqualAsync(FileInfo file1, FileInfo file2, CancellationToken cancellationToken);

}
