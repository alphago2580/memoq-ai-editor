using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace SidecarApp
{
    public partial class MainWindow : Window
    {
        private NamedPipeServer? _pipeServer;
        private KeyboardInjector? _injector;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize WebView2
                await MainWebView.EnsureCoreWebView2Async(null);

                // Set up message handler from JavaScript
                MainWebView.CoreWebView2.WebMessageReceived += WebView_MessageReceived;

                // Load the editor UI
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Frontend", "index.html");
                if (File.Exists(htmlPath))
                {
                    MainWebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                }
                else
                {
                    MessageBox.Show($"Frontend not found at: {htmlPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Start Named Pipe server to receive data from MemoQ Plugin
                _pipeServer = new NamedPipeServer();
                _pipeServer.DataReceived += PipeServer_DataReceived;
                _pipeServer.Start();

                // Initialize keyboard injector
                _injector = new KeyboardInjector();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PipeServer_DataReceived(object? sender, string data)
        {
            // Forward data from Plugin to WebView2
            Dispatcher.Invoke(() =>
            {
                try
                {
                    MainWebView.CoreWebView2.PostWebMessageAsJson(data);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to forward pipe data: {ex.Message}");
                }
            });
        }

        private void WebView_MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(message);

                if (data?.action == "Inject")
                {
                    string translation = data.content;
                    _injector?.InjectTranslationToMemoQ(translation);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView message handling failed: {ex.Message}");
            }
        }

        // Window drag support
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _pipeServer?.Stop();
            base.OnClosed(e);
        }
    }
}
