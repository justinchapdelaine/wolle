using System;
using System.Windows;

namespace wolle
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Check if a file path was passed as argument
            if (e.Args.Length > 0)
            {
                string filePath = e.Args[0];
                
                // Create main window and process the file
                var mainWindow = new MainWindow();
                mainWindow.Show();
                
                // Process the file after window is shown
                mainWindow.ProcessFile(filePath);
            }
            else
            {
                // No file argument - show error and exit
                MessageBox.Show("Please run this application by right-clicking a file and selecting 'Untangle the Wolle'.", 
                    "No File Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
            }
        }
    }
}