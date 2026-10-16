namespace FolderSynchronizer.Comparers;

public class FastComparer : IFileComparer
{
    public Task<bool> IsFilesEqual(FileInfo file1, FileInfo file2, CancellationToken cancellationToken)
    {
        if (!file2.Exists || file1.LastWriteTime > file2.LastWriteTime || file1.Length != file2.Length)
        {
            return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }
}
