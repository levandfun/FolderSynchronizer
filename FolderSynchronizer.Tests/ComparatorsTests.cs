
using System.Security.Cryptography;
using FolderSynchronizer.Abstraction;
using FolderSynchronizer.Comparers;
using FolderSynchronizer.Enums;

namespace FolderSynchronizer.Tests;

public class ComparatorsTests : IDisposable
{
    private readonly string _testDir;
    public ComparatorsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ComparerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }
    [Theory]
    [InlineData(EComparator.MD5)]
    [InlineData(EComparator.SHA256)]
    [InlineData(EComparator.BINARY)]
    [InlineData(EComparator.NONE)]
    public async Task AreFilesEqualAsync_WhenFilesAreIdentical_ReturnsTrue(EComparator comparatorType)
    {
        FileInfo file1 = await CreateTestFileAsync("source.txt", "Same Content 123");
        FileInfo file2 = await CreateTestFileAsync("replica.txt", "Same Content 123");

        IFileComparer sut = CreateComparer(comparatorType);

        bool result = await sut.AreFilesEqualAsync(file1, file2, CancellationToken.None);


        Assert.True(result);
    }

    [Theory]
    [InlineData(EComparator.MD5)]
    [InlineData(EComparator.SHA256)]
    [InlineData(EComparator.BINARY)]
    [InlineData(EComparator.NONE)]
    public async Task AreFilesEqualAsync_WhenLengthsDifferOrReplicaMissing_ReturnsFalse(EComparator comparatorType)
    {
        FileInfo file1 = await CreateTestFileAsync("source.txt", "Longer Content");
        FileInfo file2 = await CreateTestFileAsync("replica.txt", "Short");
        FileInfo missingFile = new(Path.Combine(_testDir, "non_existent.txt"));

        IFileComparer sut = CreateComparer(comparatorType);

        Assert.False(await sut.AreFilesEqualAsync(file1, file2, CancellationToken.None));
        Assert.False(await sut.AreFilesEqualAsync(file1, missingFile, CancellationToken.None));
    }

    [Theory]
    [InlineData(EComparator.MD5, false)]
    [InlineData(EComparator.SHA256, false)]
    [InlineData(EComparator.BINARY, false)]
    [InlineData(EComparator.NONE, true)]
    public async Task AreFilesEqualAsync_WhenSameSizeButDifferentContent_ReturnsExpectedResult(
        EComparator comparatorType,
        bool expectedResult)
    {
        FileInfo file1 = await CreateTestFileAsync("source.txt", "AAAA_");
        FileInfo file2 = await CreateTestFileAsync("replica.txt", "BBBB_");

        IFileComparer sut = CreateComparer(comparatorType);

        bool actualResult = await sut.AreFilesEqualAsync(file1, file2, CancellationToken.None);

        Assert.Equal(expectedResult, actualResult);
    }
    private static IFileComparer CreateComparer(EComparator comparator) => comparator switch
    {
        EComparator.MD5 => new HashComparer(MD5.HashDataAsync),
        EComparator.SHA256 => new HashComparer(SHA256.HashDataAsync),
        EComparator.BINARY => new BinaryComparer(),
        EComparator.NONE => new FastComparer(),
        _ => throw new ArgumentOutOfRangeException(nameof(comparator))
    };
    private async Task<FileInfo> CreateTestFileAsync(string fileName, string content)
    {
        string path = Path.Combine(_testDir, fileName);
        await File.WriteAllTextAsync(path, content);
        return new FileInfo(path);
    }
    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }
}
