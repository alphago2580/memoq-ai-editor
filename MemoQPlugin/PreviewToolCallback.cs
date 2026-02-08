using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using MemoQ.PreviewInterfaces;
using Newtonsoft.Json;

namespace MemoQAISidecarPlugin
{
    /// <summary>
    /// Handles Preview Tool callbacks from MemoQ and sends data to Sidecar via Named Pipe.
    /// </summary>
    public class PreviewToolCallback : IPreviewToolCallback, IDisposable
    {
        private const string PIPE_NAME = "MemoQ_Sidecar_Pipe";
        private NamedPipeClientStream _pipeClient;

        public PreviewToolCallback()
        {
            InitializePipeClient();
        }

        private void InitializePipeClient()
        {
            try
            {
                _pipeClient = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out, PipeOptions.Asynchronous);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoQ Plugin] Pipe client init failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Triggered when user moves cursor to a new segment in MemoQ.
        /// </summary>
        public void HandleChangeHighlightRequest(ChangeHighlightRequestFromMQ request)
        {
            try
            {
                if (request.ActivePreviewParts == null || request.ActivePreviewParts.Length == 0)
                    return;

                var part = request.ActivePreviewParts[0];

                // Extract segment data
                var payload = new
                {
                    Type = "SegmentUpdate",
                    Source = part.SourceContent?.Content ?? "",
                    Target = part.TargetContent?.Content ?? "",
                    SourceLang = part.SourceLangCode,
                    TargetLang = part.TargetLangCode,
                    Timestamp = DateTime.UtcNow.ToString("o")
                };

                // Send to Sidecar asynchronously (non-blocking)
                Task.Run(() => SendToPipeAsync(payload));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoQ Plugin] HandleChangeHighlight failed: {ex.Message}");
            }
        }

        private async Task SendToPipeAsync(object payload)
        {
            try
            {
                // Connect to Sidecar if not already connected
                if (!_pipeClient.IsConnected)
                {
                    await _pipeClient.ConnectAsync(1000); // 1 second timeout
                }

                // Serialize and send
                string json = JsonConvert.SerializeObject(payload);
                byte[] data = Encoding.UTF8.GetBytes(json);

                await _pipeClient.WriteAsync(data, 0, data.Length);
                await _pipeClient.FlushAsync();

                Debug.WriteLine($"[MemoQ Plugin] Sent data to Sidecar: {json}");
            }
            catch (TimeoutException)
            {
                Debug.WriteLine("[MemoQ Plugin] Sidecar not available (timeout).");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoQ Plugin] Pipe send failed: {ex.Message}");
                // Recreate pipe client on failure
                _pipeClient?.Dispose();
                InitializePipeClient();
            }
        }

        public void Dispose()
        {
            _pipeClient?.Dispose();
        }

        // Unused Preview SDK methods
        public void HandleContentChangedInActiveDocument(ContentChangedInActiveDocumentFromMQ request) { }
        public void HandleCustomCommand(CustomCommandFromMQ request) { }
        public void HandleProjectClosed(ProjectClosedFromMQ request) { }
        public void HandleProjectOpened(ProjectOpenedFromMQ request) { }
        public void HandleSegmentConfirmed(SegmentConfirmedFromMQ request) { }
    }
}
