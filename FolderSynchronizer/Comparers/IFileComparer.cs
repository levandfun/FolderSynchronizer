namespace FolderSynchronizer.Comparers;

public interface IFileComparer
{
    public Task<bool> IsFilesEqual(FileInfo file1, FileInfo file2, CancellationToken cancellationToken);
}
