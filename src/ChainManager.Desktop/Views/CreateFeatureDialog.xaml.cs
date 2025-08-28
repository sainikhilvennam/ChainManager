using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ChainManager.Desktop.Views;

public partial class CreateFeatureDialog : Window, INotifyPropertyChanged
{
    private string _jiraId = "";
    private string _featureName = "";
    private string? _selectedTargetProject;

    public CreateFeatureDialog(List<string> availableProjects, List<string> availableForks, List<string> availableBranches)
    {
        InitializeComponent();
        
        AvailableForks = new ObservableCollection<string>((availableForks ?? new List<string>()).OrderBy(f => f));
        AvailableBranches = new ObservableCollection<string>((availableBranches ?? new List<string>()).OrderBy(b => b));
        AvailableModes = new ObservableCollection<string> { "source", "binary", "ignore" };
        AvailableProjectNames = new ObservableCollection<string>(availableProjects.OrderBy(p => p));
        
        AvailableProjects = new ObservableCollection<ProjectSelectionItem>();
        foreach (var project in availableProjects.OrderBy(p => p))
        {
            AvailableProjects.Add(new ProjectSelectionItem { ProjectName = project, IsSelected = false });
        }
        
        DataContext = this;
    }

    public string JiraId
    {
        get => _jiraId;
        set
        {
            _jiraId = value;
            OnPropertyChanged();
        }
    }

    public string FeatureName
    {
        get => _featureName;
        set
        {
            _featureName = value;
            OnPropertyChanged();
        }
    }

    public string? SelectedTargetProject
    {
        get => _selectedTargetProject;
        set
        {
            _selectedTargetProject = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ProjectSelectionItem> AvailableProjects { get; private set; }
    public ObservableCollection<string> AvailableForks { get; private set; }
    public ObservableCollection<string> AvailableBranches { get; private set; }
    public ObservableCollection<string> AvailableModes { get; private set; }
    public ObservableCollection<string> AvailableProjectNames { get; private set; }

    public List<string> SelectedProjects => AvailableProjects.Where(p => p.IsSelected).Select(p => p.ProjectName).ToList();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var project in AvailableProjects)
            project.IsSelected = true;
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var project in AvailableProjects)
            project.IsSelected = false;
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(JiraId))
        {
            MessageBox.Show("Please enter a JIRA ID.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            JiraIdTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedTargetProject))
        {
            MessageBox.Show("Please select a target project.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            TargetProjectComboBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ProjectSelectionItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _useTests = true;
    private string? _selectedFork;
    private string? _selectedBranch;
    private string _selectedMode = "source";

    public string ProjectName { get; set; } = "";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string? SelectedFork
    {
        get => _selectedFork;
        set
        {
            _selectedFork = value;
            OnPropertyChanged();
        }
    }

    public string? SelectedBranch
    {
        get => _selectedBranch;
        set
        {
            _selectedBranch = value;
            OnPropertyChanged();
        }
    }

    public string SelectedMode
    {
        get => _selectedMode;
        set
        {
            _selectedMode = value;
            OnPropertyChanged();
        }
    }

    public bool UseTests
    {
        get => _useTests;
        set
        {
            _useTests = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}