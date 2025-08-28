using ChainManager.Core.Models;
using ChainManager.Core.Services;

namespace ChainManager.Core.Interfaces;

public interface IChainConfigurationService
{
    ChainConfiguration LoadChainFile(string filePath);
    void SaveChainFile(ChainConfiguration chainFile);
    bool ValidateChainFile(ChainConfiguration chainFile);
    bool ValidateChainFile(ChainConfiguration chainFile, out List<string> errors);
    ChainConfiguration CreateChainForFeature(string jiraId, List<string> projectNames);
    ChainConfiguration CreateChainForFeature(string jiraId, string? featureName, List<object> projectSelections, string? targetProject = null);
    void RebaseChain(ChainConfiguration chainFile, string newVersion);
    void RebaseChain(ChainConfiguration chainFile, string newVersion, Dictionary<string, string> projectVersions);
    void CreateBranchInProject(string projectName, string branchName);
    void ToggleTests(ChainConfiguration chainFile, bool enabled);
    void ToggleTests(ChainConfiguration chainFile, List<string> projectNames, bool enabled);
    void SwitchMode(ChainConfiguration chainFile, ProjectMode mode);
    void SwitchMode(ChainConfiguration chainFile, List<string> projectNames, ProjectMode mode);
    
    AnalysisReport GetAnalysisReport();
    List<string> GetKnownProjects();
    List<string> GetKnownForks();
    List<string> GetKnownBranches();
    List<string> GetKnownTags();
}