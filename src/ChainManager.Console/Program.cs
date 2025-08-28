using ChainManager.Core.Services;
using ChainManager.Core.Models;

namespace ChainManager.Console;

class Program
{
    static void Main(string[] args)
    {
        var chainService = new ChainConfigurationService();
        var consoleInterface = new ConsoleInterface(chainService);
        consoleInterface.Run();
    }
}

public class ConsoleInterface
{
    private readonly ChainConfigurationService _chainService;

    public ConsoleInterface(ChainConfigurationService chainService)
    {
        _chainService = chainService;
    }

    public void Run()
    {
        System.Console.WriteLine("Chain Manager - Console Mode");
        System.Console.WriteLine("=============================");
        
        while (true)
        {
            ShowMenu();
            var choice = System.Console.ReadLine();
            
            try
            {
                switch (choice)
                {
                    case "1": ValidateChainFile(); break;
                    case "2": CreateChainForFeature(); break;
                    case "3": RebaseChain(); break;
                    case "4": ToggleTests(); break;
                    case "5": SwitchMode(); break;
                    case "6": return;
                    default: System.Console.WriteLine("Invalid option. Try again."); break;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
    
    private void ShowMenu()
    {
        System.Console.Clear();
        System.Console.WriteLine("1. Validate Chain File");
        System.Console.WriteLine("2. Create Chain for Feature");
        System.Console.WriteLine("3. Rebase Chain");
        System.Console.WriteLine("4. Toggle Tests");
        System.Console.WriteLine("5. Switch Mode");
        System.Console.WriteLine("6. Exit");
        System.Console.Write("\nSelect option: ");
    }

    private void ValidateChainFile()
    {
        System.Console.Write("Enter chain file path (or just filename): ");
        var input = System.Console.ReadLine() ?? "";
        
        var filePath = input.Contains("\\") ? input : Path.Combine(@"C:\Users\vsainikhil\source\Chains", input);
        if (!filePath.EndsWith(".properties")) filePath += ".properties";
        
        var chainFile = _chainService.LoadChainFile(filePath);
        var isValid = _chainService.ValidateChainFile(chainFile, out var errors);
        
        System.Console.WriteLine($"Chain file '{chainFile.JiraId}' is {(isValid ? "valid" : "invalid")}");
        if (!isValid)
        {
            foreach (var error in errors)
                System.Console.WriteLine($"  - {error}");
        }
        System.Console.WriteLine($"Projects: {chainFile.Projects.Count}");
        System.Console.WriteLine($"Global version: {chainFile.GlobalVersion}");
        foreach (var project in chainFile.Projects.Values.Take(5))
        {
            var forkInfo = !string.IsNullOrEmpty(project.Fork) ? $" [fork: {project.Fork}]" : "";
            System.Console.WriteLine($"  {project.ProjectName}: {project.Mode} ({project.Branch ?? project.Tag ?? "default"}){forkInfo}");
        }
        if (chainFile.Projects.Count > 5)
            System.Console.WriteLine($"  ... and {chainFile.Projects.Count - 5} more projects");
        
        System.Console.WriteLine("\nPress any key to continue...");
        System.Console.ReadKey();
    }

    private void CreateChainForFeature()
    {
        System.Console.Write("Enter JIRA ID (e.g., 12345 or DEPM-12345): ");
        var jiraId = System.Console.ReadLine() ?? "";
        
        System.Console.Write("Enter feature name: ");
        var featureName = System.Console.ReadLine() ?? "";
        
        // Load template first to show available projects
        var templatePath = Path.Combine(@"C:\Users\vsainikhil\source\Chains", "$feature-template.properties");
        if (File.Exists(templatePath))
        {
            var template = _chainService.LoadChainFile(templatePath);
            System.Console.WriteLine($"\nAvailable projects ({template.Projects.Count}):");
            foreach (var proj in template.Projects.Keys)
                System.Console.WriteLine($"  {proj}");
        }
        
        System.Console.Write("\nEnter project name for this feature: ");
        var projectName = System.Console.ReadLine() ?? "";
        
        try
        {
            var chainFile = _chainService.CreateChainForFeature(jiraId, new List<string>());
            
            // Add DEPM- prefix if not present
            var fullJiraId = jiraId.StartsWith("DEPM-") ? jiraId : $"DEPM-{jiraId}";
            var branchName = $"dev/{fullJiraId.ToLower()}-{featureName.Replace(" ", "-").Replace("_", "-").ToLower()}";
            
            // Set branch for specified project
            if (chainFile.Projects.ContainsKey(projectName))
            {
                chainFile.Projects[projectName].Branch = branchName;
                chainFile.Projects[projectName].Fork = "<firstname.lastname>/" + projectName;
                
                _chainService.SaveChainFile(chainFile);
                
                System.Console.WriteLine($"\nChain file created: {Path.GetFileName(chainFile.FilePath)}");
                System.Console.WriteLine($"Project: {projectName}");
                System.Console.WriteLine($"Branch: {branchName}");
            }
            else
            {
                System.Console.WriteLine($"\nError: Project '{projectName}' not found in template.");
                System.Console.WriteLine("Please use one of the available projects listed above.");
            }
        }
        catch (InvalidOperationException ex)
        {
            System.Console.WriteLine($"Error: {ex.Message}");
        }
        
        System.Console.WriteLine("\nPress any key to continue...");
        System.Console.ReadKey();
    }

    private void RebaseChain()
    {
        System.Console.Write("Enter chain file path (or just filename): ");
        var input = System.Console.ReadLine() ?? "";
        
        var filePath = input.Contains("\\") ? input : Path.Combine(@"C:\Users\vsainikhil\source\Chains", input);
        if (!filePath.EndsWith(".properties")) filePath += ".properties";
        
        System.Console.Write("Enter new version: ");
        var newVersion = System.Console.ReadLine() ?? "";
        
        var chainFile = _chainService.LoadChainFile(filePath);
        _chainService.RebaseChain(chainFile, newVersion);
        _chainService.SaveChainFile(chainFile);
        
        System.Console.WriteLine($"Chain '{chainFile.JiraId}' rebased to version {newVersion}");
        System.Console.WriteLine("\nPress any key to continue...");
        System.Console.ReadKey();
    }

    private void ToggleTests()
    {
        System.Console.Write("Enter chain file path (or just filename): ");
        var input = System.Console.ReadLine() ?? "";
        
        var filePath = input.Contains("\\") ? input : Path.Combine(@"C:\Users\vsainikhil\source\Chains", input);
        if (!filePath.EndsWith(".properties")) filePath += ".properties";
        
        var chainFile = _chainService.LoadChainFile(filePath);
        
        while (true)
        {
            System.Console.Clear();
            System.Console.WriteLine($"Toggle Tests - {Path.GetFileName(chainFile.FilePath)}");
            System.Console.WriteLine("=".PadRight(50, '='));
            
            foreach (var project in chainFile.Projects.Values)
            {
                System.Console.WriteLine($"{project.ProjectName}: {(project.TestsUnit ? "enabled" : "disabled")}");
            }
            
            System.Console.WriteLine();
            System.Console.Write("Enter project name (or 'exit' to finish): ");
            var projectName = System.Console.ReadLine() ?? "";
            
            if (projectName.ToLower() == "exit") break;
            
            if (!chainFile.Projects.ContainsKey(projectName))
            {
                System.Console.WriteLine($"Project '{projectName}' not found.");
                continue;
            }
            
            System.Console.Write("Enable tests? (y/n): ");
            var enableTests = System.Console.ReadLine()?.ToLower() == "y";
            
            _chainService.ToggleTests(chainFile, new List<string> { projectName }, enableTests);
            System.Console.WriteLine($"Tests {(enableTests ? "enabled" : "disabled")} for {projectName}");
        }
        
        _chainService.SaveChainFile(chainFile);
        System.Console.WriteLine("Changes saved.");
    }
    
    private void SwitchMode()
    {
        System.Console.Write("Enter chain file path (or just filename): ");
        var input = System.Console.ReadLine() ?? "";
        
        var filePath = input.Contains("\\") ? input : Path.Combine(@"C:\Users\vsainikhil\source\Chains", input);
        if (!filePath.EndsWith(".properties")) filePath += ".properties";
        
        var chainFile = _chainService.LoadChainFile(filePath);
        
        while (true)
        {
            System.Console.Clear();
            System.Console.WriteLine($"Switch Mode - {Path.GetFileName(chainFile.FilePath)}");
            System.Console.WriteLine("=".PadRight(50, '='));
            
            foreach (var project in chainFile.Projects.Values)
            {
                System.Console.WriteLine($"{project.ProjectName}: {project.Mode}");
            }
            
            System.Console.WriteLine();
            System.Console.Write("Enter project name (or 'exit' to finish): ");
            var projectName = System.Console.ReadLine() ?? "";
            
            if (projectName.ToLower() == "exit") break;
            
            if (!chainFile.Projects.ContainsKey(projectName))
            {
                System.Console.WriteLine($"Project '{projectName}' not found.");
                continue;
            }
            
            System.Console.Write("Enter mode (source/binary): ");
            var mode = System.Console.ReadLine()?.ToLower();
            
            if (mode == "source" || mode == "binary")
            {
                chainFile.Projects[projectName].Mode = mode;
                System.Console.WriteLine($"Mode set to {mode} for {projectName}");
            }
            else
            {
                System.Console.WriteLine("Invalid mode. Use 'source' or 'binary'.");
            }
        }
        
        _chainService.SaveChainFile(chainFile);
        System.Console.WriteLine("Changes saved.");
        System.Console.WriteLine("\nPress any key to continue...");
        System.Console.ReadKey();
    }
}