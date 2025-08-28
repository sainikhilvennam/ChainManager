using ChainManager.Core.Models;

namespace ChainManager.Core.Services;

public class ChainFileAnalyzer
{
    private const string ChainFilesPath = @"C:\Users\vsainikhil\source\Chains";
    
    public HashSet<string> ValidProjects { get; private set; } = new();
    public HashSet<string> ValidForks { get; private set; } = new();
    public HashSet<string> ValidBranches { get; private set; } = new();
    public HashSet<string> ValidTags { get; private set; } = new();
    public HashSet<string> ValidModes { get; private set; } = new() { "source", "binary", "ignore", "binary,source", "source,binary" };
    
    public void AnalyzeAllChainFiles()
    {
        if (!Directory.Exists(ChainFilesPath))
        {
            Console.WriteLine($"Chains directory not found: {ChainFilesPath}");
            return;
        }

        var chainFiles = Directory.GetFiles(ChainFilesPath, "*.properties");
        Console.WriteLine($"Analyzing {chainFiles.Length} chain files...");

        foreach (var filePath in chainFiles)
        {
            try
            {
                AnalyzeChainFile(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        Console.WriteLine($"Analysis complete:");
        Console.WriteLine($"  Projects: {ValidProjects.Count}");
        Console.WriteLine($"  Forks: {ValidForks.Count}");
        Console.WriteLine($"  Branches: {ValidBranches.Count}");
        Console.WriteLine($"  Tags: {ValidTags.Count}");
    }

    private void AnalyzeChainFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("#") || string.IsNullOrWhiteSpace(trimmedLine)) continue;
            
            var parts = trimmedLine.Split('=', 2);
            if (parts.Length != 2) continue;
            
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            
            if (string.IsNullOrEmpty(value) || value.StartsWith("<")) continue;
            
            ExtractDataFromProperty(key, value);
        }
    }

    private void ExtractDataFromProperty(string key, string value)
    {
        if (key.StartsWith("global.")) return;
        
        if (key.Contains("."))
        {
            var parts = key.Split('.');
            var projectName = parts[0];
            var property = string.Join(".", parts.Skip(1));
            
            ValidProjects.Add(projectName);
            
            switch (property)
            {
                case "fork":
                    ValidForks.Add(value);
                    break;
                case "branch":
                    ValidBranches.Add(value);
                    break;
                case "tag":
                    ValidTags.Add(value);
                    break;
            }
        }
    }

    public AnalysisReport GetAnalysisReport()
    {
        return new AnalysisReport
        {
            ProjectCount = ValidProjects.Count,
            ForkCount = ValidForks.Count,
            BranchCount = ValidBranches.Count,
            TagCount = ValidTags.Count,
            Projects = ValidProjects.ToList(),
            Forks = ValidForks.ToList(),
            Branches = ValidBranches.ToList(),
            Tags = ValidTags.ToList()
        };
    }
}

public class AnalysisReport
{
    public int ProjectCount { get; set; }
    public int ForkCount { get; set; }
    public int BranchCount { get; set; }
    public int TagCount { get; set; }
    public List<string> Projects { get; set; } = new();
    public List<string> Forks { get; set; } = new();
    public List<string> Branches { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}