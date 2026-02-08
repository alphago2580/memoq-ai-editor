# Deployment Guide

## Pre-Deployment Checklist

- [ ] Build completed without errors
- [ ] Ollama installed and running
- [ ] MemoQ installed
- [ ] Frontend files bundled
- [ ] Plugin DLL built

## Deployment Steps

### 1. Deploy MemoQ Plugin

1. Build the plugin:
```powershell
cd MemoQPlugin
dotnet build -c Release
```

2. Locate the DLL:
```
MemoQPlugin\bin\Release\net48\MemoQAISidecarPlugin.dll
```

3. Copy to MemoQ plugin directory:
```powershell
Copy-Item "bin\Release\net48\MemoQAISidecarPlugin.dll" "$env:APPDATA\MemoQ\Addins\"
```

4. Restart MemoQ

5. Enable plugin:
   - Open MemoQ
   - Go to **Tools > Options > Preview**
   - Check **"AI Sidecar Assistant"**

### 2. Deploy Sidecar Application

1. Build the Sidecar:
```powershell
cd SidecarApp
dotnet publish -c Release -r win-x64 --self-contained false
```

2. Package contents:
```
SidecarApp\bin\Release\net6.0-windows\publish\
├── SidecarApp.exe
├── SidecarApp.dll
├── Microsoft.Web.WebView2.*.dll
├── Newtonsoft.Json.dll
└── Frontend\
    ├── index.html
    ├── style.css
    └── editor.bundle.js
```

3. Create installer (optional):
   - Use WiX Toolset or Inno Setup
   - Create Start Menu shortcut
   - Add to Windows startup (optional)

### 3. Setup Ollama

1. Install Ollama:
```powershell
winget install Ollama.Ollama
```

2. Pull required model:
```bash
ollama pull llama2
# Or for better translation:
ollama pull mistral
```

3. Configure as Windows service (optional):
```powershell
# Create service using NSSM (Non-Sucking Service Manager)
nssm install OllamaService "C:\Users\<YourUser>\AppData\Local\Programs\Ollama\ollama.exe" serve
nssm start OllamaService
```

## Configuration

### Production Settings

#### 1. Disable DevTools
In `SidecarApp/MainWindow.xaml.cs`, remove or comment:
```csharp
// MainWebView.CoreWebView2.OpenDevToolsWindow();
```

#### 2. Adjust AI Parameters
In `SidecarApp/OllamaClient.cs`:
```csharp
options = new
{
    temperature = 0.3,  // Lower = more deterministic
    top_p = 0.9,
    max_tokens = 150    // Increase for longer suggestions
}
```

#### 3. Configure Window Behavior
In `SidecarApp/MainWindow.xaml`:
```xml
<!-- For overlay mode -->
Topmost="True"
WindowStyle="None"
AllowsTransparency="True"

<!-- For normal window mode -->
Topmost="False"
WindowStyle="SingleBorderWindow"
AllowsTransparency="False"
```

## User Documentation

Create a quick start guide for end users:

### Quick Start for Translators

1. **Start Ollama** (automatic if configured as service)

2. **Launch Sidecar**
   - Double-click `SidecarApp.exe`
   - Window should appear with "Ready" status

3. **Open MemoQ**
   - Load your translation project
   - Ensure "AI Sidecar Assistant" is enabled in Preview settings

4. **Start Translating**
   - Navigate to any segment in MemoQ
   - Sidecar automatically updates with source text
   - Type your translation
   - Press `Tab` to accept AI suggestions
   - Press `Ctrl+Enter` to send translation to MemoQ

5. **Tips**
   - Wait ~500ms after typing for AI suggestions
   - Use `Ctrl+Enter` to quickly move to next segment
   - Keep Sidecar window visible for best workflow

## Monitoring and Logs

### Enable Logging

Add to `SidecarApp/App.xaml.cs`:
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    // Setup file logging
    var logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MemoQSidecar",
        "logs",
        $"sidecar_{DateTime.Now:yyyyMMdd}.log"
    );

    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
    Trace.Listeners.Add(new TextWriterTraceListener(logPath));
    Trace.AutoFlush = true;
}
```

### Log Locations
- **Sidecar**: `%LOCALAPPDATA%\MemoQSidecar\logs\`
- **MemoQ Plugin**: Use DebugView from Sysinternals
- **Ollama**: Check console output or Windows Event Viewer

## Troubleshooting

### Common Issues

#### Sidecar doesn't receive segments
1. Check MemoQ plugin is enabled
2. Verify Named Pipe name matches in both Plugin and Sidecar
3. Run Sidecar as Administrator if pipe connection fails

#### AI not responding
1. Check Ollama status: `curl http://localhost:11434/api/tags`
2. Verify model is downloaded: `ollama list`
3. Check Ollama logs for errors

#### Injection fails
1. Ensure MemoQ window is active
2. Check clipboard access permissions
3. Verify SendKeys is not blocked by security software

## Security Considerations

### Production Environment

1. **Named Pipe Security**
   - Configure ACLs to restrict pipe access
   - Use encryption for sensitive data

2. **Ollama Network Binding**
   - Keep Ollama on `localhost` only
   - Do not expose port 11434 externally

3. **Clipboard Data**
   - Clear clipboard after injection
   - Avoid logging translation content

## Performance Tuning

### Optimize AI Response Time

1. **Use faster models**:
```bash
ollama pull tinyllama  # Faster but less accurate
ollama pull mistral     # Good balance
```

2. **Reduce debounce delay** in `Frontend/editor.js`:
```javascript
setTimeout(async () => { ... }, 300);  // Reduced from 500ms
```

3. **Enable GPU acceleration** for Ollama:
```bash
# Ensure CUDA is installed for NVIDIA GPUs
# Ollama auto-detects GPU
```

## Backup and Recovery

### Backup Important Files
```powershell
# Configuration
Copy-Item "$env:APPDATA\MemoQ\Addins\MemoQAISidecarPlugin.dll" ".\backup\"

# User settings (if implemented)
Copy-Item "$env:LOCALAPPDATA\MemoQSidecar\config.json" ".\backup\"
```

## Update Process

1. **Stop all components**:
   - Close MemoQ
   - Close Sidecar
   - Stop Ollama service (if applicable)

2. **Backup current installation**

3. **Replace files**:
   - Update Plugin DLL
   - Update Sidecar executable
   - Update Frontend files

4. **Restart components**

5. **Verify functionality**

## Support

For deployment issues, check:
- GitHub Issues: [Repository URL]
- Documentation: README.md
- Logs: Check all log locations mentioned above
