using FolderSynchronizer.Abstraction;

namespace FolderSynchronizer.Comparers;

public class BinaryComparer : FileComparerBase
{
    const int BufferSize = 1024 * 1024; // 1MB
    protected override async Task<bool> AreContentsEqualAsync(FileInfo file1, FileInfo file2, CancellationToken cancellationToken)
    {

        byte[] buffer1 = new byte[BufferSize];
        byte[] buffer2 = new byte[BufferSize];

        using FileStream fs1 = new(file1.FullName, FileMode.Open, FileAccess.Read, FileShare.Read); //ensuring that files wont be changed during the check.
        using FileStream fs2 = new(file2.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        int bytesRead1, bytesRead2;
        while ((bytesRead1 = await fs1.ReadAsync(buffer1.AsMemory(0, BufferSize), cancellationToken)) > 0 &&
               (bytesRead2 = await fs2.ReadAsync(buffer2.AsMemory(0, BufferSize), cancellationToken)) > 0)
        {
            if (bytesRead1 != bytesRead2 || !buffer1.AsSpan(0, bytesRead1).SequenceEqual(buffer2.AsSpan(0, bytesRead2)))
            {
                return false;
            }
        }
        return true;
    }
}
