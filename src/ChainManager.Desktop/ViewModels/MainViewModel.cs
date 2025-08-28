using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using ChainManager.Core.Interfaces;
using ChainManager.Core.Models;
using ChainManager.Core.Services;
using ChainManager.Desktop.Commands;

namespace ChainManager.Desktop.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IChainConfigurationService _chainService;
    private ChainConfiguration? _currentChain;
    private string _statusMessage = "Ready";
    private string _analysisInfo = "Loading analysis...";

    private readonly ParallelGitService _gitService;

    public MainViewModel()
    {
        _gitService = new ParallelGitService();
        _chainService = new ChainConfigurationService(_gitService);
        
        LoadChainCommand = new RelayCommand(_ => LoadChain());
        ValidateChainCommand = new RelayCommand(_ => ValidateChain(), _ => _currentChain != null);
        CreateFeatureCommand = new RelayCommand(_ => _ = CreateFeatureAsync());
        RebaseChainCommand = new RelayCommand(_ => RebaseChain(), _ => _currentChain != null);
        SaveChainCommand = new RelayCommand(_ => SaveChain(), _ => _currentChain != null);
        CloneRepositoriesCommand = new RelayCommand(_ => _ = CloneRepositoriesAsync());
        UpdateRepositoriesCommand = new RelayCommand(_ => _ = UpdateRepositoriesAsync());
        ShowGitDataCommand = new RelayCommand(_ => ShowGitData());

        
        Projects = new ObservableCollection<ProjectViewModel>();
        AvailableModes = new ObservableCollection<string> { "source", "binary", "ignore" };
        AvailableForks = new ObservableCollection<string>();
        AvailableBranches = new ObservableCollection<string>();
        
        // Load analysis report on startup
        LoadAnalysisReport();
        LoadAvailableOptions();
    }

    public ChainConfiguration? CurrentChain
    {
        get => _currentChain;
        set
        {
            _currentChain = value;
            OnPropertyChanged();
            UpdateProjects();
            RefreshCommandStates();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string AnalysisInfo
    {
        get => _analysisInfo;
        set
        {
            _analysisInfo = value;
            OnPropertyChanged();
        }
    }
    
    public string ConfigurationSummary
    {
        get
        {
            if (CurrentChain?.Projects == null || !CurrentChain.Projects.Any())
                return "No projects loaded";
                
            var totalProjects = CurrentChain.Projects.Count;
            var activeProjects = CurrentChain.Projects.Values.Count(p => p.IsSelected);
            var sourceMode = CurrentChain.Projects.Values.Count(p => p.IsSelected && p.Mode == "source");
            var binaryMode = CurrentChain.Projects.Values.Count(p => p.IsSelected && p.Mode == "binary");
            var testsEnabled = CurrentChain.Projects.Values.Count(p => p.IsSelected && p.TestsUnit);
            
            return $"Total: {totalProjects}\n" +
                   $"Active: {activeProjects}\n" +
                   $"Source Mode: {sourceMode}\n" +
                   $"Binary Mode: {binaryMode}\n" +
                   $"Tests Enabled: {testsEnabled}";
        }
    }

    public ObservableCollection<ProjectViewModel> Projects { get; }
    public ObservableCollection<string> AvailableModes { get; }
    public ObservableCollection<string> AvailableForks { get; }
    public ObservableCollection<string> AvailableBranches { get; }

    public ICommand LoadChainCommand { get; }
    public ICommand ValidateChainCommand { get; }
    public ICommand CreateFeatureCommand { get; }
    public ICommand RebaseChainCommand { get; }
    public ICommand SaveChainCommand { get; }
    public ICommand CloneRepositoriesCommand { get; }
    public ICommand UpdateRepositoriesCommand { get; }
    public ICommand ShowGitDataCommand { get; }


    private void LoadChain()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Properties files (*.properties)|*.properties|All files (*.*)|*.*",
            InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chains")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                CurrentChain = _chainService.LoadChainFile(dialog.FileName);
                LoadAvailableOptions();
                StatusMessage = $"Loaded: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading file: {ex.Message}";
            }
        }
    }



    private void ValidateChain()
    {
        if (CurrentChain == null) return;

        var isValid = _chainService.ValidateChainFile(CurrentChain, out var errors);
        StatusMessage = isValid ? "Chain file is valid" : $"Validation errors: {string.Join(", ", errors)}";
    }

    private async Task CreateFeatureAsync()
    {
        try
        {
            StatusMessage = "Loading project data...";
            
            var availableProjects = await Task.Run(() => _chainService.GetKnownProjects());
            var availableForks = await Task.Run(() => _chainService.GetKnownForks());
            var availableBranches = await Task.Run(() => _chainService.GetKnownBranches());
            
            StatusMessage = "Ready";
            
            var dialog = new ChainManager.Desktop.Views.CreateFeatureDialog(availableProjects, availableForks, availableBranches);
            
            if (dialog.ShowDialog() == true)
            {
                StatusMessage = "Creating chain file...";
                
                var jiraId = dialog.JiraId;
                var featureName = dialog.FeatureName;
                var chain = await Task.Run(() => 
                {
                    try
                    {
                        var projectSelections = dialog.AvailableProjects
                            .Select(p => (p.ProjectName, p.IsSelected, p.SelectedMode, p.SelectedFork, p.SelectedBranch, p.UseTests))
                            .ToList();
                        var newChain = _chainService.CreateChainForFeature(jiraId, projectSelections);
                        
                        // Update filename and branch if feature name provided
                        if (!string.IsNullOrWhiteSpace(featureName))
                        {
                            var normalizedJiraId = jiraId.StartsWith("DEPM-") ? jiraId : $"DEPM-{jiraId}";
                            var fileName = $"{normalizedJiraId}-{featureName}.properties";
                            newChain.FilePath = Path.Combine(Path.GetDirectoryName(newChain.FilePath)!, fileName);
                            
                            // Update branch names to include feature name
                            foreach (var project in newChain.Projects.Values)
                            {
                                project.Branch = $"dev/{normalizedJiraId}-{featureName}";
                            }
                        }
                        System.Diagnostics.Debug.WriteLine($"Created chain with FilePath: {newChain.FilePath}");
                        
                        _chainService.SaveChainFile(newChain);
                        System.Diagnostics.Debug.WriteLine($"Saved chain file to: {newChain.FilePath}");
                        
                        if (File.Exists(newChain.FilePath))
                        {
                            System.Diagnostics.Debug.WriteLine($"File exists: {newChain.FilePath}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"File NOT found: {newChain.FilePath}");
                        }
                        
                        return newChain;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in chain creation: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                        throw;
                    }
                });
                
                CurrentChain = chain;
                LoadAvailableOptions();
                StatusMessage = $"Created and loaded feature chain: {Path.GetFileName(chain.FilePath)} - Ready for editing";
            }
            else
            {
                StatusMessage = "Ready";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating feature: {ex.Message}";
            System.Windows.MessageBox.Show($"Error creating feature chain:\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void RebaseChain()
    {
        if (CurrentChain == null) return;
        
        var currentVersion = CurrentChain.GlobalVersion ?? "";
        var dialog = new ChainManager.Desktop.Views.InputDialog(
            $"Enter new version number:\n\nCurrent version: {currentVersion}", 
            currentVersion);
        dialog.Title = "Rebase Chain";
            
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputValue) && dialog.InputValue != currentVersion)
        {
            try
            {
                _chainService.RebaseChain(CurrentChain, dialog.InputValue);
                OnPropertyChanged(nameof(CurrentChain));
                OnPropertyChanged(nameof(ConfigurationSummary));
                StatusMessage = $"Chain rebased to version {dialog.InputValue}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error rebasing chain: {ex.Message}";
                System.Windows.MessageBox.Show($"Error rebasing chain:\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void SaveChain()
    {
        if (CurrentChain == null) return;

        try
        {
            _chainService.SaveChainFile(CurrentChain);
            StatusMessage = "Chain file saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving file: {ex.Message}";
        }
    }

    private void UpdateProjects()
    {
        Projects.Clear();
        
        if (CurrentChain == null)
            return;
            
        // Get all available projects from analyzer
        var allProjects = _chainService.GetKnownProjects();
        
        foreach (var projectName in allProjects.OrderBy(p => p))
        {
            ProjectConfiguration projectConfig;
            
            if (CurrentChain.Projects.ContainsKey(projectName))
            {
                // Project exists in chain file
                projectConfig = CurrentChain.Projects[projectName];
            }
            else
            {
                // Project doesn't exist in chain file, create default
                projectConfig = new ProjectConfiguration
                {
                    ProjectName = projectName,
                    Mode = "source",
                    IsSelected = false,
                    TestsUnit = true
                };
                CurrentChain.Projects[projectName] = projectConfig;
            }
            
            var projectViewModel = new ProjectViewModel(projectConfig);
            projectViewModel.PropertyChanged += (s, e) => OnPropertyChanged(nameof(ConfigurationSummary));
            Projects.Add(projectViewModel);
        }
        
        OnPropertyChanged(nameof(ConfigurationSummary));
    }
    
    private void RefreshCommandStates()
    {
        // Force command state refresh
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    private void LoadAnalysisReport()
    {
        try
        {
            var report = _chainService.GetAnalysisReport();
            AnalysisInfo = $"Analysis: {report.ProjectCount} projects, {report.BranchCount} branches, {report.ForkCount} forks, {report.TagCount} tags";
        }
        catch (Exception ex)
        {
            AnalysisInfo = $"Analysis failed: {ex.Message}";
        }
    }

    private void LoadAvailableOptions()
    {
        try
        {
            var forks = _chainService.GetKnownForks();
            var branches = _chainService.GetKnownBranches();
            
            AvailableForks.Clear();
            AvailableBranches.Clear();
            
            foreach (var fork in forks.OrderBy(f => f))
                AvailableForks.Add(fork);
                
            foreach (var branch in branches.OrderBy(b => b))
                AvailableBranches.Add(branch);
        }
        catch (Exception ex)
        {
            // Handle error silently, dropdowns will just be empty
        }
    }

    private async Task CloneRepositoriesAsync()
    {
        var startTime = DateTime.Now;
        var totalRepos = 62; // 17 main + 45 forks
        var completedRepos = 0;
        
        StatusMessage = $"Starting clone of {totalRepos} repositories...";
        
        try
        {
            var progress = new Progress<string>(msg => 
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    if (msg.Contains("✓ Cloned") || msg.Contains("Skipping"))
                    {
                        completedRepos++;
                        var elapsed = DateTime.Now - startTime;
                        var avgTimePerRepo = elapsed.TotalSeconds / completedRepos;
                        var remainingRepos = totalRepos - completedRepos;
                        var estimatedTimeLeft = TimeSpan.FromSeconds(avgTimePerRepo * remainingRepos);
                        
                        StatusMessage = $"{msg} | Cloned: {completedRepos} | Pending: {remainingRepos} | Elapsed: {elapsed:mm\\:ss} | ETA: {estimatedTimeLeft:mm\\:ss}";
                    }
                    else
                    {
                        var elapsed = DateTime.Now - startTime;
                        var remainingRepos = totalRepos - completedRepos;
                        StatusMessage = $"{msg} | Cloned: {completedRepos} | Pending: {remainingRepos} | Elapsed: {elapsed:mm\\:ss}";
                    }
                });
            });
            
            await Task.Run(async () => await _gitService.CloneAllRepositoriesAsync(progress));
            
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                var totalTime = DateTime.Now - startTime;
                StatusMessage = $"All {totalRepos} repositories cloned successfully! Total time: {totalTime:mm\\:ss}";
                LoadAnalysisReport();
                LoadAvailableOptions();
            });
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                var elapsed = DateTime.Now - startTime;
                StatusMessage = $"Error after {elapsed:mm\\:ss}: {ex.Message}";
                System.Windows.MessageBox.Show($"Error cloning repositories:\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            });
        }
    }

    private async Task UpdateRepositoriesAsync()
    {
        StatusMessage = "Updating repositories...";
        try
        {
            var progress = new Progress<string>(msg => 
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    StatusMessage = msg;
                });
            });
            
            await Task.Run(async () => await _gitService.UpdateAllRepositoriesAsync(progress));
            
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                StatusMessage = "All repositories updated successfully!";
                LoadAnalysisReport();
                LoadAvailableOptions();
            });
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                StatusMessage = $"Error updating repositories: {ex.Message}";
                System.Windows.MessageBox.Show($"Error updating repositories:\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            });
        }
    }

    private void ShowGitData()
    {
        try
        {
            var projects = _chainService.GetKnownProjects();
            var forks = _chainService.GetKnownForks();
            var branches = _chainService.GetKnownBranches();
            var report = _chainService.GetAnalysisReport();
            
            var details = $"Git Repository Analysis:\n\n" +
                         $"Projects ({projects.Count}):\n{string.Join(", ", projects.Take(20))}" +
                         (projects.Count > 20 ? "..." : "") + "\n\n" +
                         $"Branches ({branches.Count}):\n{string.Join(", ", branches.Take(15))}" +
                         (branches.Count > 15 ? "..." : "") + "\n\n" +
                         $"Forks ({forks.Count}):\n{string.Join(", ", forks.Take(10))}" +
                         (forks.Count > 10 ? "..." : "");
            
            System.Windows.MessageBox.Show(details, "Git Repository Data", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error getting Git data: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void ShowAnalysis()
    {
        try
        {
            var report = _chainService.GetAnalysisReport();
            var details = $"Chain Files Analysis Report:\n\n" +
                         $"Projects ({report.ProjectCount}):\n{string.Join(", ", report.Projects.Take(20))}" +
                         (report.ProjectCount > 20 ? "..." : "") + "\n\n" +
                         $"Branches ({report.BranchCount}):\n{string.Join(", ", report.Branches.Take(15))}" +
                         (report.BranchCount > 15 ? "..." : "") + "\n\n" +
                         $"Forks ({report.ForkCount}):\n{string.Join(", ", report.Forks.Take(10))}" +
                         (report.ForkCount > 10 ? "..." : "") + "\n\n" +
                         $"Tags ({report.TagCount}):\n{string.Join(", ", report.Tags.Take(10))}" +
                         (report.TagCount > 10 ? "..." : "");
            
            System.Windows.MessageBox.Show(details, "Chain Files Analysis", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error getting analysis: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ProjectViewModel : INotifyPropertyChanged
{
    private readonly ProjectConfiguration _project;

    public ProjectViewModel(ProjectConfiguration project)
    {
        _project = project;
    }

    public string ProjectName => _project.ProjectName;
    public string Mode
    {
        get => _project.Mode;
        set
        {
            _project.Mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusInfo));
        }
    }

    public string? Fork
    {
        get => _project.Fork;
        set
        {
            _project.Fork = value;
            OnPropertyChanged();
        }
    }

    public string? Branch
    {
        get => _project.Branch;
        set
        {
            _project.Branch = value;
            OnPropertyChanged();
        }
    }

    public bool TestsUnit
    {
        get => _project.TestsUnit;
        set
        {
            _project.TestsUnit = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusInfo));
        }
    }

    public bool IsSelected
    {
        get => _project.IsSelected;
        set
        {
            _project.IsSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusInfo));
        }
    }
    
    public string StatusInfo
    {
        get
        {
            var status = IsSelected ? "Active" : "Inactive";
            var testStatus = TestsUnit ? "Tests: On" : "Tests: Off";
            return $"{status}, {testStatus}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}