using ChainManager.Core.Models;
using ChainManager.Core.Interfaces;
using System.Text;

namespace ChainManager.Core.Services;

public class ChainConfigurationService : IChainConfigurationService
{
    private static readonly string ChainFilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chains");
    private readonly ParallelGitService _gitService;
    private readonly ChainFileAnalyzer _analyzer;
    private readonly object _lock = new object();
    private bool _dataAnalyzed = false;

    public ChainConfigurationService(ParallelGitService gitService)
    {
        _gitService = gitService;
        _analyzer = new ChainFileAnalyzer(gitService);
        Directory.CreateDirectory(ChainFilesPath);
        AnalyzeChainFiles();
    }

    public ChainConfiguration LoadChainFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Chain file not found: {filePath}");

        var content = File.ReadAllText(filePath);
        return ParseChainContent(content, filePath);
    }



    private ChainConfiguration ParseChainContent(string content, string identifier)
    {
        var chainFile = new ChainConfiguration { FilePath = identifier };
        var lines = content.Split('\n');
        
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
        
        chainFile.JiraId = ExtractJiraId(identifier);
        return chainFile;
    }

    private string ExtractJiraId(string identifier)
    {
        // Extract JIRA ID from various formats:
        // "path/to/DEPM-123.properties" -> "DEPM-123"
        // "project:feature/DEPM-123" -> "DEPM-123"
        // "fork/project:feature/DEPM-123" -> "DEPM-123"
        var fileName = Path.GetFileNameWithoutExtension(identifier);
        if (fileName.Contains('-')) return fileName;
        
        var parts = identifier.Split(':');
        if (parts.Length > 1)
        {
            var branch = parts[1];
            if (branch.Contains('/')) branch = branch.Split('/').Last();
            return branch;
        }
        
        return fileName;
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
        
        // Analysis will be refreshed on next access
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

            var forkError = ValidateFork(project.Fork ?? "");
            if (!string.IsNullOrEmpty(forkError))
                errors.Add($"{project.ProjectName}: {forkError}");

            var branchError = ValidateBranch(project.Branch ?? "");
            if (!string.IsNullOrEmpty(branchError))
                errors.Add($"{project.ProjectName}: {branchError}");

            var tagError = ValidateTag(project.Tag ?? "");
            if (!string.IsNullOrEmpty(tagError))
                errors.Add($"{project.ProjectName}: {tagError}");
        }
        
        return errors.Count == 0;
    }

    public ChainConfiguration CreateChainForFeature(string jiraId, List<(string ProjectName, bool IsSelected, string Mode, string? Fork, string? Branch, bool UseTests)> projectSelections)
    {
        // Ensure JIRA ID starts with DEPM
        var normalizedJiraId = jiraId.StartsWith("DEPM-") ? jiraId : $"DEPM-{jiraId}";
        
        var chainFile = new ChainConfiguration { JiraId = normalizedJiraId, FilePath = Path.Combine(ChainFilesPath, $"{normalizedJiraId}.properties") };
        
        foreach (var selection in projectSelections)
        {
            var project = new ProjectConfiguration 
            { 
                ProjectName = selection.ProjectName,
                Mode = selection.Mode,
                IsSelected = selection.IsSelected,
                TestsUnit = selection.UseTests
            };
            
            // Only set fork if user selected one (not empty or template)
            if (!string.IsNullOrWhiteSpace(selection.Fork) && !selection.Fork.StartsWith("<"))
            {
                project.Fork = selection.Fork;
            }
            
            // Only set branch if user selected one
            if (!string.IsNullOrWhiteSpace(selection.Branch))
            {
                project.Branch = selection.Branch;
            }
            
            chainFile.Projects[selection.ProjectName] = project;
        }
        
        return chainFile;
    }

    public ChainConfiguration CreateChainForFeature(string jiraId, List<string> projectNames)
    {
        // Legacy method - convert to ProjectSelectionItem format
        var selections = projectNames.Select(name => (name, true, "source", (string?)null, (string?)null, true)).ToList();
        
        return CreateChainForFeature(jiraId, selections);
    }

    public ChainConfiguration CreateChainForFeature(string jiraId, string? featureName, List<object> projectSelections, string? targetProject = null)
    {
        return CreateChainForFeature(jiraId, projectSelections.Cast<string>().ToList());
    }

    public void RebaseChain(ChainConfiguration chainFile, string newVersion)
    {
        chainFile.GlobalVersion = newVersion;
        chainFile.GlobalDevsVersion = newVersion;
    }

    public void RebaseChain(ChainConfiguration chainFile, string newVersion, Dictionary<string, string> projectVersions)
    {
        RebaseChain(chainFile, newVersion);
    }

    public void CreateBranchInProject(string projectName, string branchName)
    {
        var branches = _gitService.GetBranches(projectName);
    }

    public void ToggleTests(ChainConfiguration chainFile, bool enabled)
    {
        foreach (var project in chainFile.Projects.Values)
            project.TestsUnit = enabled;
    }

    public void ToggleTests(ChainConfiguration chainFile, List<string> projectNames, bool enabled)
    {
        foreach (var projectName in projectNames)
            if (chainFile.Projects.ContainsKey(projectName))
                chainFile.Projects[projectName].TestsUnit = enabled;
    }

    public void SwitchMode(ChainConfiguration chainFile, ProjectMode mode)
    {
        foreach (var project in chainFile.Projects.Values)
            project.Mode = mode.ToString().ToLower();
    }

    public void SwitchMode(ChainConfiguration chainFile, List<string> projectNames, ProjectMode mode)
    {
        foreach (var projectName in projectNames)
            if (chainFile.Projects.ContainsKey(projectName))
                chainFile.Projects[projectName].Mode = mode.ToString().ToLower();
    }

    public AnalysisReport GetAnalysisReport() => _analyzer.GetAnalysisReport();
    public List<string> GetKnownProjects() => _analyzer.ValidProjects.ToList();
    public List<string> GetKnownForks() => _analyzer.ValidForks.ToList();
    public List<string> GetKnownBranches() => _analyzer.ValidBranches.ToList();
    public List<string> GetKnownTags() => _analyzer.ValidTags.ToList();

    private string ValidateMode(string mode) => _analyzer.ValidModes.Contains(mode) ? "" : "Invalid mode";
    private string ValidateProject(string project) => _analyzer.ValidProjects.Contains(project) ? "" : "Unknown project";
    private string ValidateFork(string fork) => string.IsNullOrEmpty(fork) || _analyzer.ValidForks.Contains(fork) ? "" : "Unknown fork";
    private string ValidateBranch(string branch) => string.IsNullOrEmpty(branch) || _analyzer.ValidBranches.Contains(branch) ? "" : "Unknown branch";
    private string ValidateTag(string tag) => string.IsNullOrEmpty(tag) || _analyzer.ValidTags.Contains(tag) ? "" : "Unknown tag";
}