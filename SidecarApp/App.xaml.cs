using System;
using System.Windows;

namespace SidecarApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set up global exception handler
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"Unhandled exception: {ex?.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }
    }
}
