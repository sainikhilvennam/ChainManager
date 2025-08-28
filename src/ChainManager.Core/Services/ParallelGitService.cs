using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using ChainManager.Core.Models;

namespace ChainManager.Core.Services;

public class ParallelGitService
{
    private static readonly string ReposBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repos");
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repositories.json");
    private const int MaxConcurrency = 8;
    private RepositoryConfig? _config;

    public async Task CloneAllRepositoriesAsync(IProgress<string>? progress = null)
    {
        var allRepos = GetAllRepositories();
        
        Directory.CreateDirectory(Path.Combine(ReposBasePath, "main"));
        Directory.CreateDirectory(Path.Combine(ReposBasePath, "forks"));

        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var tasks = allRepos.Select(async repo =>
        {
            await semaphore.WaitAsync();
            try
            {
                await CloneRepositoryAsync(repo.Url, repo.LocalPath, progress);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        progress?.Report("All repositories cloned successfully!");
    }

    public async Task UpdateAllRepositoriesAsync(IProgress<string>? progress = null)
    {
        var existingRepos = GetExistingRepositories();
        
        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var tasks = existingRepos.Select(async repoPath =>
        {
            await semaphore.WaitAsync();
            try
            {
                await UpdateRepositoryAsync(repoPath, progress);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        progress?.Report("All repositories updated successfully!");
    }

    private async Task CloneRepositoryAsync(string repoUrl, string localPath, IProgress<string>? progress)
    {
        if (Directory.Exists(localPath))
        {
            progress?.Report($"Skipping {Path.GetFileName(localPath)} - already exists");
            return;
        }

        progress?.Report($"Cloning {Path.GetFileName(localPath)}...");
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone --bare {repoUrl} \"{localPath}\"",
                WorkingDirectory = ReposBasePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            progress?.Report($"✓ Cloned {Path.GetFileName(localPath)}");
        else
            progress?.Report($"✗ Failed to clone {Path.GetFileName(localPath)}");
    }

    private async Task UpdateRepositoryAsync(string repoPath, IProgress<string>? progress)
    {
        progress?.Report($"Updating {Path.GetFileName(repoPath)}...");
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "fetch --all",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            progress?.Report($"✓ Updated {Path.GetFileName(repoPath)}");
        else
            progress?.Report($"✗ Failed to update {Path.GetFileName(repoPath)}");
    }

    public RepositoryConfig LoadConfig()
    {
        if (_config != null) return _config;
        
        if (!File.Exists(ConfigPath))
            throw new FileNotFoundException($"Repository config not found: {ConfigPath}");
            
        var json = File.ReadAllText(ConfigPath);
        _config = JsonSerializer.Deserialize<RepositoryConfig>(json) ?? new RepositoryConfig();
        return _config;
    }

    private List<(string Url, string LocalPath)> GetAllRepositories()
    {
        var config = LoadConfig();
        var repos = new List<(string, string)>();

        // Main repositories
        foreach (var url in config.MainRepositories)
        {
            var projectName = ExtractProjectName(url);
            var localPath = Path.Combine(ReposBasePath, "main", $"{projectName}.git");
            repos.Add((url, localPath));
        }

        // Fork repositories
        foreach (var fork in config.ForkRepositories)
        {
            var forkName = fork.Key;
            var forkDir = Path.Combine(ReposBasePath, "forks", forkName);
            
            foreach (var url in fork.Value)
            {
                var projectName = ExtractProjectName(url);
                var localPath = Path.Combine(forkDir, $"{projectName}.git");
                repos.Add((url, localPath));
            }
        }

        return repos;
    }

    private List<string> GetExistingRepositories()
    {
        var repos = new List<string>();
        
        var mainDir = Path.Combine(ReposBasePath, "main");
        if (Directory.Exists(mainDir))
            repos.AddRange(Directory.GetDirectories(mainDir));

        var forksDir = Path.Combine(ReposBasePath, "forks");
        if (Directory.Exists(forksDir))
        {
            foreach (var forkDir in Directory.GetDirectories(forksDir))
                repos.AddRange(Directory.GetDirectories(forkDir));
        }

        return repos;
    }

    public string ExtractProjectName(string repoUrl)
    {
        return Path.GetFileNameWithoutExtension(repoUrl.Split('/').Last());
    }

    public List<string> GetBranches(string projectName, string? forkName = null)
    {
        var repoPath = forkName == null
            ? Path.Combine(ReposBasePath, "main", $"{projectName}.git")
            : Path.Combine(ReposBasePath, "forks", forkName, $"{projectName}.git");

        if (!Directory.Exists(repoPath)) return new List<string>();

        var result = ExecuteGit("branch -r", repoPath);
        return result.Split('\n')
            .Where(b => !string.IsNullOrEmpty(b.Trim()))
            .Select(b => b.Trim().Replace("origin/", ""))
            .Where(b => b != "HEAD")
            .ToList();
    }



    private string ExecuteGit(string arguments, string workingDirectory)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}