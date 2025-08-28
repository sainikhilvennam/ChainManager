using ChainManager.Core.Services;

namespace ChainManager;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--console")
        {
            RunConsoleMode();
            return;
        }
        
        // Try to start WPF desktop application
        try
        {
            var app = new System.Windows.Application();
            var mainWindow = new ChainManager.Desktop.Views.MainWindow();
            app.Run(mainWindow);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting desktop app: {ex.Message}");
            Console.WriteLine("Starting console mode...");
            RunConsoleMode();
        }
    }
    
    private static void RunConsoleMode()
    {
        var chainService = new ChainConfigurationService();
        var consoleInterface = new ChainManager.Console.ConsoleInterface(chainService);
        consoleInterface.Run();
    }
}