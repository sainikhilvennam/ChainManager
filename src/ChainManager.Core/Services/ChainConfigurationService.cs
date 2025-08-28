using ChainManager.Core.Models;
using ChainManager.Core.Interfaces;
using System.Text;

namespace ChainManager.Core.Services;

public class ChainConfigurationService : IChainConfigurationService
{
    private const string ChainFilesPath = @"C:\Users\vsainikhil\source\Chains";
    private readonly ChainFileAnalyzer _analyzer;
    private readonly object _lock = new object();
    private bool _dataAnalyzed = false;

    public ChainConfigurationService()
    {
        _analyzer = new ChainFileAnalyzer();
        AnalyzeChainFiles();
    }

    public ChainConfiguration LoadChainFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Chain file not found: {filePath}");

        var chainFile = new ChainConfiguration { FilePath = filePath };
        string[] lines;
        
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Access denied reading chain file '{filePath}'. Check file permissions.", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Error reading chain file '{filePath}'. File may be in use or corrupted.", ex);
        }
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("#") || string.IsNullOrWhiteSpace(trimmedLine)) continue;
            
            var parts = trimmedLine.Split('=', 2);
            if (parts.Length != 2) continue;
            
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            
            ParseProperty(chainFile, key, value);
        }
        
        chainFile.JiraId = Path.GetFileNameWithoutExtension(filePath);
        return chainFile;
    }

    private void ParseProperty(ChainConfiguration chainFile, string key, string value)
    {
        if (key.StartsWith("global."))
        {
            switch (key)
            {
                case "global.version.binary": chainFile.GlobalVersion = value; break;
                case "global.devs.version.binary": chainFile.GlobalDevsVersion = value; break;
                default: chainFile.GlobalProperties[key] = value; break;
            }
        }
        else if (key.Contains("."))
        {
            var parts = key.Split('.');
            var projectName = parts[0];
            var property = string.Join(".", parts.Skip(1));
            
            if (!chainFile.Projects.ContainsKey(projectName))
                chainFile.Projects[projectName] = new ProjectConfiguration { ProjectName = projectName };
            
            var project = chainFile.Projects[projectName];
            
            switch (property)
            {
                case "mode": project.Mode = value; break;
                case "mode.devs": project.ModeDevs = value; break;
                case "fork": project.Fork = value; break;
                case "branch": project.Branch = value; break;
                case "tag": project.Tag = value; break;
                case "tests.unit": project.TestsUnit = ParseBool(value); break;
                case "test.units": project.TestsUnit = ParseBool(value); break;
                default:
                    if (property.EndsWith(".run"))
                        project.TestSets[property] = ParseBool(value);
                    else
                        project.CustomProperties[property] = value;
                    break;
            }
        }
    }
    
    private bool ParseBool(string value) => 
        value.ToLower() is "true" or "yes" or "1";

    public void SaveChainFile(ChainConfiguration chainFile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Chain configuration file");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(chainFile.GlobalVersion))
            sb.AppendLine($"global.version.binary={chainFile.GlobalVersion}");
        if (!string.IsNullOrEmpty(chainFile.GlobalDevsVersion))
            sb.AppendLine($"global.devs.version.binary={chainFile.GlobalDevsVersion}");
        
        foreach (var globalProp in chainFile.GlobalProperties)
            sb.AppendLine($"{globalProp.Key}={globalProp.Value}");
        
        foreach (var project in chainFile.Projects.Values.OrderBy(p => p.ProjectName))
        {
            sb.AppendLine();
            
            var basePrefix = project.IsSelected ? "" : "#";
            
            sb.AppendLine($"{basePrefix}{project.ProjectName}.mode={project.Mode}");
            if (!string.IsNullOrEmpty(project.ModeDevs))
                sb.AppendLine($"{basePrefix}{project.ProjectName}.mode.devs={project.ModeDevs}");
            
            // Fork: only uncomment if project is selected AND has a real fork value (not template)
            var hasFork = !string.IsNullOrEmpty(project.Fork) && !project.Fork.StartsWith("<");
            var forkPrefix = (project.IsSelected && hasFork) ? "" : "#";
            var forkValue = hasFork ? project.Fork : $"<firstname.lastname>/{project.ProjectName}";
            sb.AppendLine($"{forkPrefix}{project.ProjectName}.fork={forkValue}");
                
            // Branch: only uncomment if project is selected AND has a real branch value
            var hasBranch = !string.IsNullOrEmpty(project.Branch);
            var branchPrefix = (project.IsSelected && hasBranch) ? "" : "#";
            var branchValue = hasBranch ? project.Branch : "integration";
            sb.AppendLine($"{branchPrefix}{project.ProjectName}.branch={branchValue}");
                
            // Tag: only uncomment if has actual tag value
            if (!string.IsNullOrEmpty(project.Tag))
                sb.AppendLine($"{basePrefix}{project.ProjectName}.tag={project.Tag}");
            else
                sb.AppendLine($"#{project.ProjectName}.tag=Build_12.25.1.{chainFile.GlobalVersion}");
            
            var testsPrefix = project.IsSelected ? "" : "#";
            sb.AppendLine($"{testsPrefix}{project.ProjectName}.tests.unit={project.TestsUnit.ToString().ToLower()}");
            
            foreach (var testSet in project.TestSets)
                sb.AppendLine($"{testsPrefix}{project.ProjectName}.{testSet.Key}={testSet.Value.ToString().ToLower()}");
                
            foreach (var customProp in project.CustomProperties)
                sb.AppendLine($"{basePrefix}{project.ProjectName}.{customProp.Key}={customProp.Value}");
        }
        
        try
        {
            File.WriteAllText(chainFile.FilePath, sb.ToString());
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Access denied writing chain file '{chainFile.FilePath}'. Check file permissions.", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Error writing chain file '{chainFile.FilePath}'. File may be in use or disk full.", ex);
        }
        
        // Refresh analysis to include newly created branches/forks
        lock (_lock)
        {
            _dataAnalyzed = false;
        }
        AnalyzeChainFiles();
    }

    public bool ValidateChainFile(ChainConfiguration chainFile) => ValidateChainFile(chainFile, out _);
    
    private void AnalyzeChainFiles()
    {
        lock (_lock)
        {
            if (!_dataAnalyzed)
            {
                _analyzer.AnalyzeAllChainFiles();
                _dataAnalyzed = true;
            }
        }
    }

    public bool ValidateChainFile(ChainConfiguration chainFile, out List<string> errors)
    {
        errors = new List<string>();
        
        if (string.IsNullOrEmpty(chainFile.JiraId))
            errors.Add("Missing JIRA ID");
            
        if (!chainFile.Projects.Any())
            errors.Add("No projects found");
        
        foreach (var project in chainFile.Projects.Values)
        {
            if (string.IsNullOrEmpty(project.ProjectName))
                errors.Add($"Project has empty name");
                
            var modeError = ValidateMode(project.Mode);
            if (!string.IsNullOrEmpty(modeError))
                errors.Add($"{project.ProjectName}: {modeError}");

            var projectError = ValidateProject(project.ProjectName);
            if (!string.IsNullOrEmpty(projectError))
                errors.Add($"{project.ProjectName}: {projectError}");

            var forkError = ValidateFork(project.Fork);
            if (!string.IsNullOrEmpty(forkError))
                errors.Add($"{project.ProjectName}: {forkError}");

            var branchError = ValidateBranch(project.Branch);
            if (!string.IsNullOrEmpty(branchError))
                errors.Add($"{project.ProjectName}: {branchError}");

            var tagError = ValidateTag(project.Tag);
            if (!string.IsNullOrEmpty(tagError))
                errors.Add($"{project.ProjectName}: {tagError}");
        }
        
        return !errors.Any();
    }

    private string? ValidateMode(string mode)
    {
        if (_analyzer.ValidModes.Contains(mode))
            return null;
        return $"Invalid mode '{mode}'. Valid modes: {string.Join(", ", _analyzer.ValidModes)}";
    }

    private string? ValidateProject(string projectName)
    {
        if (string.IsNullOrEmpty(projectName))
            return "Project name cannot be empty";
            
        if (_analyzer.ValidProjects.Contains(projectName))
            return null;
            
        return $"Unknown project '{projectName}'. Known projects: {string.Join(", ", _analyzer.ValidProjects.Take(5))}...";
    }

    private string? ValidateFork(string? fork)
    {
        if (string.IsNullOrEmpty(fork) || _analyzer.ValidForks.Contains(fork))
            return null;
            
        // Template patterns should be commented out, not used as actual values
        if (fork.StartsWith("<") && fork.EndsWith(">"))
            return $"Template fork '{fork}' should be commented out or replaced with actual fork name";
            
        // Allow common fork patterns (username/repo format)
        if (fork.Contains("/") && fork.Split('/').Length == 2)
            return null;
            
        return $"Unknown fork '{fork}'. Consider using a known fork pattern";
    }

    private string? ValidateBranch(string? branch)
    {
        if (string.IsNullOrEmpty(branch) || branch.StartsWith("<") || _analyzer.ValidBranches.Contains(branch))
            return null;
            
        // Allow common branch patterns
        var commonPatterns = new[] { "dev/", "feature/", "bugfix/", "hotfix/", "integration", "master", "main" };
        if (commonPatterns.Any(pattern => branch.StartsWith(pattern) || branch.Equals(pattern)))
            return null;
            
        return $"Unknown branch '{branch}'. Consider using a known branch pattern";
    }

    private string? ValidateTag(string? tag)
    {
        if (string.IsNullOrEmpty(tag) || tag.StartsWith("<") || _analyzer.ValidTags.Contains(tag))
            return null;
        return $"Unknown tag '{tag}'. Consider using a known tag pattern";
    }

    public AnalysisReport GetAnalysisReport() => _analyzer.GetAnalysisReport();

    public ChainConfiguration CreateChainForFeature(string jiraId, List<string> projectNames)
    {
        var selections = projectNames.Select(p => new { ProjectName = p, IsSelected = true, UseFork = true, UseBranch = true, UseTests = true }).Cast<object>().ToList();
        return CreateChainForFeature(jiraId, null, selections, null);
    }
    
    public ChainConfiguration CreateChainForFeature(string jiraId, string? featureName, List<object> projectSelections, string? targetProject = null)
    {
        if (!jiraId.StartsWith("DEPM-"))
            jiraId = $"DEPM-{jiraId}";
            
        var existingFiles = Directory.GetFiles(ChainFilesPath, $"{jiraId}*.properties");
        if (existingFiles.Any())
            throw new InvalidOperationException($"Chain file already exists for JIRA ID {jiraId}");
        
        var fileName = string.IsNullOrEmpty(featureName) ? 
            $"{jiraId}.properties" : 
            $"{jiraId}-{featureName.Replace(" ", "-").Replace("_", "-")}.properties";
            
        var chainFile = new ChainConfiguration
        {
            JiraId = jiraId,
            FilePath = Path.Combine(ChainFilesPath, fileName),
            GlobalVersion = "20018",
            GlobalDevsVersion = "20018"
        };
        
        // Load template to get all projects
        var templatePath = Path.Combine(ChainFilesPath, "$feature-template.properties");
        if (File.Exists(templatePath))
        {
            var template = LoadChainFile(templatePath);
            foreach (var templateProject in template.Projects.Values)
            {
                chainFile.Projects[templateProject.ProjectName] = new ProjectConfiguration
                {
                    ProjectName = templateProject.ProjectName,
                    Mode = "source",
                    ModeDevs = "binary",
                    Branch = null, // Will be set later for selected projects
                    Fork = null,   // Will be set later for selected projects
                    TestsUnit = true,
                    IsSelected = false // Default to not selected
                };
            }
        }
        
        // Apply selections from dialog
        foreach (var selection in projectSelections)
        {
            // Handle both ProjectSelectionItem and anonymous objects
            string projectName;
            bool isSelected;
            string? selectedFork = null;
            string? selectedBranch = null;
            bool useTests = true;
            
            if (selection.GetType().Name == "ProjectSelectionItem")
            {
                dynamic item = selection;
                projectName = item.ProjectName;
                isSelected = item.IsSelected;
                selectedFork = item.SelectedFork;
                selectedBranch = item.SelectedBranch;
                useTests = item.UseTests;
                var selectedMode = item.SelectedMode as string ?? "source";
                
                if (chainFile.Projects.ContainsKey(projectName))
                {
                    chainFile.Projects[projectName].Mode = selectedMode;
                }
            }
            else
            {
                // Handle anonymous objects
                dynamic item = selection;
                projectName = item.ProjectName;
                isSelected = item.IsSelected;
                try { selectedFork = item.SelectedFork; } catch { }
                try { selectedBranch = item.SelectedBranch; } catch { }
                try { useTests = item.UseTests; } catch { }
            }
            
            if (chainFile.Projects.ContainsKey(projectName))
            {
                var project = chainFile.Projects[projectName];
                project.IsSelected = isSelected;
                project.TestsUnit = useTests;
                
                if (isSelected)
                {
                    // Only set branch if explicitly selected (not empty/null)
                    if (!string.IsNullOrEmpty(selectedBranch))
                        project.Branch = selectedBranch;
                    
                    // Use selected fork if provided and not a template
                    if (!string.IsNullOrEmpty(selectedFork) && !selectedFork.StartsWith("<"))
                        project.Fork = selectedFork;
                }
                
                // Set target project branch to dev/<filename> format
                if (projectName == targetProject)
                {
                    var chainFileName = Path.GetFileNameWithoutExtension(chainFile.FilePath);
                    project.Branch = $"dev/{chainFileName}";
                    project.IsSelected = true;
                }
            }
        }
        
        return chainFile;
    }

    public void RebaseChain(ChainConfiguration chainFile, string newVersion) =>
        RebaseChain(chainFile, newVersion, new Dictionary<string, string>());
    
    public void RebaseChain(ChainConfiguration chainFile, string newVersion, Dictionary<string, string> projectVersions)
    {
        chainFile.GlobalVersion = newVersion;
        chainFile.GlobalDevsVersion = newVersion;
        
        foreach (var project in chainFile.Projects.Values)
        {
            if (project.TagEnabled)
            {
                var projectVersion = projectVersions.GetValueOrDefault(project.ProjectName, newVersion);
                project.Tag = $"Build_12.25.1.{projectVersion}";
            }
        }
    }

    public void CreateBranchInProject(string projectName, string branchName) =>
        Console.WriteLine($"Creating branch '{branchName}' in project '{projectName}'");

    public void ToggleTests(ChainConfiguration chainFile, bool enabled) =>
        ToggleTests(chainFile, chainFile.Projects.Keys.ToList(), enabled);
    
    public void ToggleTests(ChainConfiguration chainFile, List<string> projectNames, bool enabled)
    {
        foreach (var projectName in projectNames.Where(chainFile.Projects.ContainsKey))
            chainFile.Projects[projectName].TestsUnit = enabled;
    }

    public void SwitchMode(ChainConfiguration chainFile, ProjectMode mode) =>
        SwitchMode(chainFile, chainFile.Projects.Keys.ToList(), mode);
    
    public void SwitchMode(ChainConfiguration chainFile, List<string> projectNames, ProjectMode mode)
    {
        var modeStr = mode.ToString().ToLower();
        foreach (var projectName in projectNames.Where(chainFile.Projects.ContainsKey))
            chainFile.Projects[projectName].Mode = modeStr;
    }

    public List<string> GetKnownProjects() => _analyzer.ValidProjects.ToList();
    public List<string> GetKnownForks() => _analyzer.ValidForks.ToList();
    public List<string> GetKnownBranches() => _analyzer.ValidBranches.ToList();
    public List<string> GetKnownTags() => _analyzer.ValidTags.ToList();
}