using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ChainManager.Desktop.Views;

public partial class InputDialog : Window, INotifyPropertyChanged
{
    private string _message = "";
    private string _inputValue = "";

    public InputDialog(string message, string defaultValue = "")
    {
        InitializeComponent();
        DataContext = this;
        Message = message;
        InputValue = defaultValue;
        
        Loaded += (s, e) => InputTextBox.Focus();
    }

    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            OnPropertyChanged();
        }
    }

    public string InputValue
    {
        get => _inputValue;
        set
        {
            _inputValue = value;
            OnPropertyChanged();
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
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