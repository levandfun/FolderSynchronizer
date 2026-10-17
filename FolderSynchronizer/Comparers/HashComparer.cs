using FolderSynchronizer.Abstraction;

namespace FolderSynchronizer.Comparers;

public class HashComparer(Func<Stream, CancellationToken, ValueTask<byte[]>> hashAlgorithmAsync) : FileComparerBase
{
    private readonly Func<Stream, CancellationToken, ValueTask<byte[]>> _hashAlgorithmAsync = hashAlgorithmAsync;

    protected override async Task<bool> AreContentsEqualAsync(FileInfo file1, FileInfo file2, CancellationToken cancellationToken)
    {
        using FileStream stream1 = new(file1.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream stream2 = new(file2.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] hash1 = await _hashAlgorithmAsync(stream1, cancellationToken);
        byte[] hash2 = await _hashAlgorithmAsync(stream2, cancellationToken);

        return hash1.SequenceEqual(hash2);
    }
}
