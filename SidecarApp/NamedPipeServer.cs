using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SidecarApp
{
    /// <summary>
    /// Named Pipe server that receives data from MemoQ Plugin.
    /// </summary>
    public class NamedPipeServer
    {
        private const string PIPE_NAME = "MemoQ_Sidecar_Pipe";
        private CancellationTokenSource? _cts;
        private Task? _serverTask;

        public event EventHandler<string>? DataReceived;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _serverTask = Task.Run(() => RunServerAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _serverTask?.Wait(TimeSpan.FromSeconds(2));
        }

        private async Task RunServerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PIPE_NAME,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(cancellationToken);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string data = await reader.ReadToEndAsync();

                    if (!string.IsNullOrWhiteSpace(data))
                    {
                        DataReceived?.Invoke(this, data);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Pipe Server] Error: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); // Wait before retry
                }
            }
        }
    }
}
