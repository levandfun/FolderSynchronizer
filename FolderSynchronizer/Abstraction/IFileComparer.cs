namespace FolderSynchronizer.Abstraction;

public interface IFileComparer
{
    public Task<bool> AreFilesEqualAsync(FileInfo file1, FileInfo file2, CancellationToken cancellationToken);
}
