# Project Structure

```
MemoQ_plugin/
├── MemoQPlugin/                    # MemoQ Preview SDK Plugin (C# .NET Framework 4.8)
│   ├── lib/                        # Place MemoQ SDK DLLs here
│   │   ├── MemoQ.PreviewInterfaces.dll
│   │   ├── MemoQ.Addins.Common.dll
│   │   └── README.txt
│   ├── PluginDirector.cs          # Plugin entry point and registration
│   ├── PreviewToolCallback.cs     # Handles cursor movement events
│   └── MemoQPlugin.csproj         # Project file
│
├── SidecarApp/                     # WPF Application (C# .NET 6)
│   ├── App.xaml                    # Application definition
│   ├── App.xaml.cs                 # Application startup logic
│   ├── MainWindow.xaml             # Main window UI (transparent overlay)
│   ├── MainWindow.xaml.cs          # Window logic and WebView2 integration
│   ├── NamedPipeServer.cs          # Receives data from MemoQ Plugin
│   ├── KeyboardInjector.cs         # WinAPI-based text injection to MemoQ
│   ├── OllamaClient.cs             # AI integration (Ollama API client)
│   └── SidecarApp.csproj           # Project file
│
├── Frontend/                       # Web UI (CodeMirror 6)
│   ├── index.html                  # Main HTML structure
│   ├── style.css                   # VS Code-inspired dark theme
│   ├── editor.js                   # CodeMirror 6 + Ghost Text logic
│   ├── package.json                # npm dependencies
│   └── editor.bundle.js            # (Generated) Bundled JavaScript
│
├── architecture.md                 # Original architecture specification
├── sdk_refer.md                    # MemoQ SDK reference
├── README.md                       # Main documentation
├── DEPLOYMENT.md                   # Deployment and configuration guide
├── PROJECT_STRUCTURE.md            # This file
├── build.ps1                       # Build script (PowerShell)
└── .gitignore                      # Git ignore rules
```

## Component Communication Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                          MemoQ Client                           │
│  (User navigates to segment with cursor)                        │
└───────────────────┬─────────────────────────────────────────────┘
                    │ Cursor Movement Event
                    ▼
┌─────────────────────────────────────────────────────────────────┐
│              MemoQPlugin (Preview SDK Callback)                 │
│  - PluginDirector: Registers as "AI Sidecar Assistant"         │
│  - PreviewToolCallback: Captures ChangeHighlightRequest         │
│  - Extracts: Source, Target, SourceLang, TargetLang             │
└───────────────────┬─────────────────────────────────────────────┘
                    │ JSON via Named Pipe
                    │ {"Type":"SegmentUpdate","Source":"..."}
                    ▼
┌─────────────────────────────────────────────────────────────────┐
│            SidecarApp (WPF Host Application)                    │
│  - NamedPipeServer: Receives segment data                       │
│  - MainWindow: Hosts WebView2 control                           │
│  - Forwards JSON to WebView2 via PostWebMessageAsJson()         │
└───────────────────┬─────────────────────────────────────────────┘
                    │ JavaScript Message
                    ▼
┌─────────────────────────────────────────────────────────────────┐
│           Frontend (CodeMirror 6 in WebView2)                   │
│  - Updates source text display panel                            │
│  - Loads target text into editor                                │
│  - User types → debounced AI request                            │
│  - AI response → Ghost Text decoration                          │
│  - Tab key → accept suggestion                                  │
│  - Ctrl+Enter → send "Inject" message to C#                     │
└───────────────────┬─────────────────────────────────────────────┘
                    │ {"action":"Inject","content":"..."}
                    ▼
┌─────────────────────────────────────────────────────────────────┐
│          KeyboardInjector (WinAPI Automation)                   │
│  1. Clipboard.SetText(translation)                              │
│  2. SetForegroundWindow(MemoQ)                                  │
│  3. SendKeys: Ctrl+A → Del → Ctrl+V → Ctrl+Enter               │
└───────────────────┬─────────────────────────────────────────────┘
                    │ Translation inserted
                    ▼
┌─────────────────────────────────────────────────────────────────┐
│                  MemoQ Client (Updated)                         │
│  Segment confirmed, cursor moves to next segment                │
└─────────────────────────────────────────────────────────────────┘
```

## Key Technologies

### MemoQ Plugin
- **SDK**: MemoQ.PreviewInterfaces, MemoQ.Addins.Common
- **Framework**: .NET Framework 4.8
- **Communication**: Named Pipe Client (async)

### Sidecar App
- **Framework**: .NET 6 (WPF)
- **UI**: WebView2 (Chromium-based)
- **IPC**: Named Pipe Server
- **WinAPI**: user32.dll (SetForegroundWindow)
- **Injection**: System.Windows.Forms.SendKeys

### Frontend
- **Editor**: CodeMirror 6
- **Language**: JavaScript (ES Modules)
- **Build**: esbuild
- **Features**: Ghost Text (StateField + Decoration.widget)

### AI Integration
- **Model**: Ollama (Local LLM)
- **Default Model**: llama2 / mistral
- **API**: REST (http://localhost:11434/api/generate)
- **Client**: HttpClient in OllamaClient.cs

## Build Order

1. **Frontend** (npm build)
   - Input: `editor.js`, `package.json`
   - Output: `editor.bundle.js`
   - Required for Sidecar runtime

2. **Sidecar** (dotnet build)
   - Copies Frontend files to `bin/.../Frontend/`
   - Produces: `SidecarApp.exe`

3. **Plugin** (dotnet build)
   - Requires MemoQ SDK DLLs in `lib/`
   - Produces: `MemoQAISidecarPlugin.dll`

## Runtime Requirements

### Development
- Visual Studio 2022 or VS Code
- .NET 6 SDK
- .NET Framework 4.8 Developer Pack
- Node.js 16+
- MemoQ (any recent version with Preview SDK support)
- Ollama

### End User
- .NET 6 Runtime (desktop)
- .NET Framework 4.8 (usually pre-installed on Windows)
- MemoQ
- Ollama
- No Node.js required (frontend is pre-bundled)

## Configuration Files

### Frontend Configuration
- `package.json`: npm dependencies and build script
- `style.css`: CSS variables for theming

### Sidecar Configuration
- `MainWindow.xaml`: Window appearance (overlay/normal)
- `OllamaClient.cs`: AI model and endpoint settings

### Plugin Configuration
- `PluginDirector.cs`: Plugin GUID and name
- `PreviewToolCallback.cs`: Named Pipe name

## Data Formats

### Pipe Message (Plugin → Sidecar)
```json
{
  "Type": "SegmentUpdate",
  "Source": "Hello world",
  "Target": "안녕하세요",
  "SourceLang": "en-US",
  "TargetLang": "ko-KR",
  "Timestamp": "2024-01-01T00:00:00.000Z"
}
```

### WebView Message (Frontend → Sidecar)
```json
{
  "action": "Inject",
  "content": "Translated text here"
}
```

### Ollama Request
```json
{
  "model": "llama2",
  "prompt": "Translate...",
  "stream": false,
  "options": {
    "temperature": 0.7,
    "top_p": 0.9,
    "max_tokens": 100
  }
}
```

## Testing Strategy

### Unit Testing
- **Plugin**: Mock IPreviewToolCallback events
- **Sidecar**: Test Named Pipe communication in isolation
- **Frontend**: Test CodeMirror extensions with jsdom

### Integration Testing
- Use PowerShell scripts to simulate MemoQ Plugin
- Test WebView2 message passing
- Verify WinAPI injection with Notepad

### End-to-End Testing
- Full MemoQ workflow with actual translation project
- Monitor Named Pipe traffic with PipeList (Sysinternals)
- Verify Ollama API calls with network monitoring

## Performance Considerations

### Plugin (Critical Path)
- **Must be non-blocking**: Fire-and-forget Named Pipe sends
- **No heavy processing**: Extract data only, don't transform
- **Target latency**: < 10ms per event

### Sidecar (Background)
- **Async pipe reads**: Don't block UI thread
- **Debounced AI requests**: 500ms delay to reduce API calls
- **Lazy loading**: Initialize components on-demand

### Frontend (User-Facing)
- **Responsive editor**: CodeMirror handles large docs efficiently
- **Smooth animations**: CSS transitions for status changes
- **Instant feedback**: Show spinner during AI requests

## Security Notes

### Named Pipe
- Runs in user context (no elevation needed)
- Local machine only (not network-accessible)
- Consider ACLs for multi-user environments

### Clipboard
- Temporary storage for injection
- Cleared after use (TODO: implement)
- May expose sensitive data to clipboard managers

### Ollama
- Localhost-only API (default)
- No authentication required
- Data never leaves local machine

## Future Enhancements

1. **Glossary Integration**
   - Load MemoQ term bases via API
   - Pass glossary context to AI

2. **Quality Assurance**
   - Spell check integration
   - Terminology consistency checks

3. **Custom Hotkeys**
   - Configurable keybindings
   - Global hotkey to show/hide Sidecar

4. **Multi-Segment Context**
   - Send previous/next segments to AI
   - Better context-aware suggestions

5. **Plugin Settings UI**
   - Configure Ollama model from MemoQ
   - Adjust AI parameters without recompiling

## Troubleshooting Guide

### Build Errors
- **"MemoQ.PreviewInterfaces not found"**: Copy SDK DLLs to `lib/`
- **"WebView2 not found"**: Run `dotnet restore` in SidecarApp
- **"esbuild not found"**: Run `npm install` in Frontend

### Runtime Errors
- **"Pipe timeout"**: Start Sidecar before opening MemoQ
- **"MemoQ not found"**: Check process name (case-sensitive)
- **"AI not responding"**: Verify Ollama is running

### Performance Issues
- **Slow AI**: Use smaller model (tinyllama)
- **High CPU**: Reduce debounce delay in editor.js
- **Memory leak**: Check WebView2 message handlers for proper cleanup
