using System.Diagnostics;

namespace FolderSynchronizer.E2ETests;

public class EndToEndTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _sourcePath;
    private readonly string _replicaPath;
    private readonly string _logFilePath;

    public EndToEndTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "E2ESyncTests_");
        _sourcePath = Path.Combine(Path.GetTempPath(), "Source");
        _replicaPath = Path.Combine(Path.GetTempPath(), "Replica");
        _logFilePath = Path.Combine(Path.GetTempPath(), "sync.log");

        Directory.CreateDirectory(_sourcePath);
        Directory.CreateDirectory(_replicaPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Application_FullEndToEndCycle_SynchronizesFoldersAndWritesLogs()
    {
        string subDir = Path.Combine(_sourcePath, "Docs");
        Directory.CreateDirectory(subDir);
        string sourceFile = Path.Combine(subDir, "readme.txt");
        await File.WriteAllTextAsync(sourceFile, "E2E Test Content");

        string orphanFile = Path.Combine(_replicaPath, "old_file.txt");
        await File.WriteAllTextAsync(orphanFile, "Delete me");

        string appDllPath = typeof(Program).Assembly.Location;

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{appDllPath}\" -s \"{_sourcePath}\" -r \"{_replicaPath}\" -l \"{_logFilePath}\" -i 5000 -c MD5",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        Assert.NotNull(process);

        await Task.Delay(1500);

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        string consoleOutput = await process.StandardOutput.ReadToEndAsync();
        string expectedReplicaFile = Path.Combine(_replicaPath, "Docs", "readme.txt");
        Assert.True(File.Exists(expectedReplicaFile));
        Assert.Equal("E2E Test Content", await File.ReadAllTextAsync(expectedReplicaFile));

        Assert.False(File.Exists(orphanFile));

        Assert.Contains("Starting synchronization", consoleOutput);
        Assert.True(File.Exists(_logFilePath));

        string logFileContent = await File.ReadAllTextAsync(_logFilePath);
        Assert.Contains("Starting synchronization", logFileContent);
        Assert.Contains("Copied", logFileContent);
        Assert.Contains("Deleted", logFileContent);
    }
}
