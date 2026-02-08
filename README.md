# MemoQ AI Sidecar Editor

AI-powered external editor for MemoQ with Ghost Text autocompletion.

## Architecture

```
MemoQ (Preview SDK) --> Named Pipe --> Sidecar (WPF + WebView2) --> AI (Ollama)
                                              |
                                              v
                                     WinAPI Injection --> MemoQ
```

## Components

### 1. MemoQ Plugin (DLL)
- **Path**: `MemoQPlugin/`
- **Role**: Listens to cursor movement events in MemoQ via Preview SDK
- **Output**: `MemoQAISidecarPlugin.dll`
- **Installation**: Copy to MemoQ plugin folder (usually `%APPDATA%\MemoQ\Addins`)

### 2. Sidecar App (WPF)
- **Path**: `SidecarApp/`
- **Role**: Hosts the WebView2 editor, manages Named Pipe communication, handles keyboard injection
- **Output**: `SidecarApp.exe`
- **Features**:
  - Transparent/overlay window support
  - WinAPI integration for focus switching
  - Named Pipe server for receiving segment data

### 3. Frontend (CodeMirror 6)
- **Path**: `Frontend/`
- **Role**: Modern code editor with Ghost Text (inline AI suggestions)
- **Technologies**: CodeMirror 6, Vanilla JS (ES Modules)
- **Features**:
  - Real-time AI suggestions (debounced)
  - Tab to accept, Ctrl+Enter to confirm
  - VS Code-inspired dark theme

## Prerequisites

1. **.NET 6 SDK** (for Sidecar)
2. **.NET Framework 4.8 SDK** (for MemoQ Plugin)
3. **Node.js 16+** (for Frontend build)
4. **MemoQ SDK** (Download from MemoQ developer portal)
5. **Ollama** (Install from https://ollama.ai)

## Build Instructions

### Quick Build (PowerShell)
```powershell
.\build.ps1
```

### Manual Build

#### 1. Frontend
```bash
cd Frontend
npm install
npm run build
```

#### 2. Sidecar App
```bash
cd SidecarApp
dotnet build -c Release
```

#### 3. MemoQ Plugin
1. Copy MemoQ SDK DLLs to `MemoQPlugin/lib/`:
   - `MemoQ.PreviewInterfaces.dll`
   - `MemoQ.Addins.Common.dll`
2. Build:
```bash
cd MemoQPlugin
dotnet build -c Release
```

## Installation

### Step 1: Install MemoQ Plugin
1. Locate the built DLL: `MemoQPlugin/bin/Release/net48/MemoQAISidecarPlugin.dll`
2. Copy to: `%APPDATA%\MemoQ\Addins\`
3. Restart MemoQ
4. Enable in: **Tools > Options > Preview > AI Sidecar Assistant**

### Step 2: Setup Ollama
```bash
# Install Ollama (Windows)
winget install Ollama.Ollama

# Start Ollama server
ollama serve

# Pull a model (in another terminal)
ollama pull llama2
```

### Step 3: Run Sidecar
```bash
cd SidecarApp/bin/Release/net6.0-windows
./SidecarApp.exe
```

## Usage

1. **Start Ollama**: `ollama serve`
2. **Launch Sidecar**: Run `SidecarApp.exe`
3. **Open MemoQ**: Load a translation project
4. **Activate Preview**: Enable "AI Sidecar Assistant" in MemoQ preview settings
5. **Navigate Segments**: Move to a segment in MemoQ
6. **Edit in Sidecar**: Type in the external editor
7. **Accept AI Suggestions**: Press `Tab` to accept Ghost Text
8. **Confirm Translation**: Press `Ctrl+Enter` to inject back to MemoQ

## Configuration

### Ollama Model Selection
Edit `SidecarApp/OllamaClient.cs`:
```csharp
public OllamaClient(string baseUrl = "http://localhost:11434", string model = "llama2")
```

Replace `"llama2"` with your preferred model (e.g., `"mistral"`, `"codellama"`).

### UI Customization
Edit `Frontend/style.css` to change colors, fonts, or layout.

## Troubleshooting

### "Sidecar not available (timeout)" in MemoQ logs
- Ensure `SidecarApp.exe` is running before opening MemoQ
- Check Windows Firewall settings for Named Pipe access

### "Frontend not found" error in Sidecar
- Run `npm run build` in the `Frontend` folder
- Verify `Frontend/editor.bundle.js` exists
- Check `SidecarApp/bin/.../Frontend/` contains HTML/CSS/JS files

### AI suggestions not working
- Verify Ollama is running: `curl http://localhost:11434/api/tags`
- Check Sidecar console for `[Ollama] Connection failed` messages
- Ensure you've pulled a model: `ollama list`

### Keyboard injection not working
- Verify MemoQ process name is "MemoQ" (case-sensitive)
- Check if MemoQ segment editor allows clipboard paste (Ctrl+V)
- Try running Sidecar as Administrator

## Architecture Details

### Named Pipe Communication
- **Pipe Name**: `MemoQ_Sidecar_Pipe`
- **Direction**: Plugin (Client) → Sidecar (Server)
- **Format**: JSON

Example message:
```json
{
  "Type": "SegmentUpdate",
  "Source": "Hello world",
  "Target": "안녕하세요",
  "SourceLang": "en-US",
  "TargetLang": "ko-KR"
}
```

### WinAPI Injection Flow
1. `SetForegroundWindow(MemoQ)` - Focus MemoQ
2. `SendKeys("^a")` - Select all text in segment
3. `SendKeys("{DEL}")` - Delete existing text
4. `SendKeys("^v")` - Paste from clipboard
5. `SendKeys("^{ENTER}")` - Confirm and move to next segment

### Ghost Text Implementation
- Uses CodeMirror 6's `StateField` + `Decoration.widget`
- Rendered as inline, non-editable text
- Cleared on any user input
- Accepted via `Tab` key

## Development

### Debug Frontend in Browser
1. Modify `MainWindow.xaml.cs` to enable DevTools:
```csharp
await MainWebView.EnsureCoreWebView2Async(null);
MainWebView.CoreWebView2.OpenDevToolsWindow();
```

2. Use browser console:
```javascript
window.debugEditor.setSource("Test source text");
window.debugEditor.setContent("Test translation");
window.debugEditor.injectGhost(" suggested continuation");
```

### Testing Without MemoQ
Use PowerShell to send test data to Named Pipe:
```powershell
$pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "MemoQ_Sidecar_Pipe", "Out")
$pipe.Connect(1000)
$writer = New-Object System.IO.StreamWriter($pipe)
$json = '{"Type":"SegmentUpdate","Source":"Hello","Target":"","SourceLang":"en","TargetLang":"ko"}'
$writer.WriteLine($json)
$writer.Flush()
$pipe.Close()
```

## License

MIT License - See LICENSE file for details

## Credits

- **MemoQ SDK**: Kilgray Translation Technologies
- **CodeMirror 6**: Marijn Haverbeke
- **WebView2**: Microsoft
- **Ollama**: Ollama Team

## Support

For issues and feature requests, please create an issue on the GitHub repository.
