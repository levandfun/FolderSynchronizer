namespace FolderSynchronizer.Comparers
{
    public class HashComparer(Func<Stream, CancellationToken, ValueTask<byte[]>> hashAlgorithm) : IFileComparer
    {
        private readonly Func<Stream, CancellationToken, ValueTask<byte[]>> _hashAlgorithm = hashAlgorithm;
        public async Task<bool> IsFilesEqual(FileInfo file1, FileInfo file2, CancellationToken cancellationToken)
        {
            if (!file2.Exists || file1.LastWriteTime > file2.LastWriteTime || file1.Length != file2.Length)
            {
                return false;
            }

            using FileStream stream1 = new(file1.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream stream2 = new(file2.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] hash1 = await _hashAlgorithm(stream1, cancellationToken);
            byte[] hash2 = await _hashAlgorithm(stream2, cancellationToken);

            return hash1.SequenceEqual(hash2);
        }

    }
}
