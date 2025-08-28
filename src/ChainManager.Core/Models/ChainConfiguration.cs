namespace ChainManager.Core.Models;

public class ChainConfiguration
{
    public string FilePath { get; set; } = string.Empty;
    public string JiraId { get; set; } = string.Empty;
    public Dictionary<string, ProjectConfiguration> Projects { get; set; } = new();
    public string GlobalVersion { get; set; } = string.Empty;
    public string GlobalDevsVersion { get; set; } = string.Empty;
    public Dictionary<string, string> GlobalProperties { get; set; } = new();
    public ChainFileType FileType { get; set; } = ChainFileType.Properties;
}

public enum ChainFileType
{
    Properties,
    WebYaml
}

public class ProjectConfiguration
{
    public string ProjectName { get; set; } = string.Empty;
    public string Mode { get; set; } = "source";
    public string ModeDevs { get; set; } = "binary";
    public string? Fork { get; set; }
    public string? Branch { get; set; }
    public string? Tag { get; set; }
    public bool TestsUnit { get; set; } = true;
    public Dictionary<string, bool> TestSets { get; set; } = new();
    public Dictionary<string, string> CustomProperties { get; set; } = new();
    public bool IsSelected { get; set; } = true;
    public bool TagEnabled { get; set; } = true;
    public bool ForkEnabled { get; set; } = true;
    public bool BranchEnabled { get; set; } = true;
    public bool TestsEnabled { get; set; } = true;
}

public enum ProjectMode
{
    Source,
    Binary,
    Ignore
}