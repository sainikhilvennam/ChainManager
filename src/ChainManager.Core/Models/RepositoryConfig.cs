namespace ChainManager.Core.Models;

public class RepositoryConfig
{
    public List<string> MainRepositories { get; set; } = new();
    public Dictionary<string, List<string>> ForkRepositories { get; set; } = new();
}