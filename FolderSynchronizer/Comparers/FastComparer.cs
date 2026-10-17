using FolderSynchronizer.Abstraction;

namespace FolderSynchronizer.Comparers;

public class FastComparer : FileComparerBase
{
    protected override Task<bool> AreContentsEqualAsync(FileInfo file1, FileInfo file2, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}
