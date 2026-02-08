MemoQ SDK & AI Sidecar Technical ReferenceThis document provides the technical specifications and implementation code snippets for building the "MemoQ AI Sidecar Editor".1. System ArchitectureThe system consists of three parts to ensure a non-blocking experience for the translator.MemoQ Plugin (DLL): Acts as a "Fake Preview Tool". Listens for cursor movement events via Preview SDK and sends data to the Sidecar via Named Pipes.Sidecar App (WPF/WebView2): Hosts the UI, communicates with the Plugin, runs the Local LLM logic, and handles Keyboard Injection back to MemoQ.Frontend (CodeMirror 6): Provides the editor UI with AI "Ghost Text" (inline completion).2. Part A: MemoQ Plugin (C# Listener)This plugin implements IPreviewToolCallback from MemoQ.PreviewInterfaces.dll.2.1. Registration (Handshake)The plugin must register itself as a preview tool to receive events.// Implementation of IPluginDirector
public class DummyPreviewToolDirector : IPluginDirector
{
    public void Initialize(IModuleEnvironment env)
    {
        // 1. Register the tool
        var registration = new RegistrationRequest {
            PreviewToolId = new Guid("YOUR-GUID-HERE"), // Must be unique
            PreviewToolName = "AI Sidecar Assistant",
            ContentComplexity = ContentComplexity.Minimal, // We need plain text
            AutoStartupCommand = "" // Optional
        };
        
        // 2. Start Named Pipe Client to talk to Sidecar
        // (Implementation details below)
    }
}
2.2. Handling Cursor Movement (Data Extraction)This method triggers whenever the user moves to a new segment. Speed is critical here.// Implementation of IPreviewToolCallback
public void HandleChangeHighlightRequest(ChangeHighlightRequestFromMQ request)
{
    if (request.ActivePreviewParts == null || request.ActivePreviewParts.Length == 0) return;

    var part = request.ActivePreviewParts[0];

    // Create a lightweight DTO to send to Sidecar
    var payload = new 
    {
        Type = "SegmentUpdate",
        Source = part.SourceContent.Content,
        Target = part.TargetContent.Content,
        SourceLang = part.SourceLangCode,
        TargetLang = part.TargetLangCode
    };

    // Fire-and-forget: Send to Sidecar via Named Pipe
    // Do NOT wait for a response to avoid freezing MemoQ
    PipeClient.SendAsync(JsonConvert.SerializeObject(payload)); 
}
3. Part B: Sidecar App (WPF Host)The Sidecar acts as the bridge between the OS, the Plugin, and the Web UI.3.1. Named Pipe Server (Listener)Receives data from the MemoQ Plugin.// In App.xaml.cs or MainViewModel
public async Task StartPipeServer()
{
    while (true)
    {
        using (var server = new NamedPipeServerStream("MemoQ_Sidecar_Pipe", PipeDirection.In))
        {
            await server.WaitForConnectionAsync();
            using (var reader = new StreamReader(server))
            {
                var message = await reader.ReadToEndAsync();
                // Forward data to WebView2
                MainWebView.CoreWebView2.PostWebMessageAsJson(message);
            }
        }
    }
}
3.2. Injection Logic (WinAPI)Injects the finished translation back into MemoQ when user presses Ctrl+Enter.using System.Runtime.InteropServices;
using System.Windows.Forms; // For SendKeys

public class Injector 
{
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    public void InjectTranslationToMemoQ(string translation)
    {
        // 1. Copy to Clipboard
        System.Windows.Clipboard.SetText(translation);

        // 2. Find MemoQ Window
        var memoqProc = Process.GetProcessesByName("MemoQ").FirstOrDefault();
        if (memoqProc != null)
        {
            // 3. Focus MemoQ
            SetForegroundWindow(memoqProc.MainWindowHandle);
            Thread.Sleep(50); // Small delay for focus switch

            // 4. Send Macros
            // Ctrl+A (Select All) -> Del (Clear) -> Ctrl+V (Paste) -> Ctrl+Enter (Confirm)
            SendKeys.SendWait("^a");
            SendKeys.SendWait("{DEL}");
            SendKeys.SendWait("^v");
            Thread.Sleep(50);
            SendKeys.SendWait("^{ENTER}");
        }
    }
}
4. Part C: Frontend (CodeMirror 6)The frontend handles the "Ghost Text" rendering.4.1. Ghost Text Extension (Concept)Use CodeMirror's StateField and Decoration to render grey text that isn't actually in the document.import { EditorView, Decoration, ViewPlugin } from "@codemirror/view";
import { StateField, StateEffect } from "@codemirror/state";

// 1. Define Ghost Text Decoration
const ghostTheme = EditorView.baseTheme({
  ".cm-ghost-text": {
    color: "#6a9955",
    fontStyle: "italic",
    pointerEvents: "none"
  }
});

const ghostDecoration = Decoration.widget({
  widget: new class extends WidgetType {
    toDOM() {
      let span = document.createElement("span");
      span.textContent = " (Tab to accept)"; // AI Suggestion here
      span.className = "cm-ghost-text";
      return span;
    }
  },
  side: 1
});

// 2. Keymap to Accept Suggestion
export const acceptGhostKeymap = {
  key: "Tab",
  run: (view) => {
    // Logic to insert the ghost text into the actual document
    // dispatch({ changes: { from: cursor, insert: suggestion } })
    return true;
  }
};
4.2. WebView2 CommunicationListen for data from the C# Sidecar.// Listen for messages from C# (WPF)
window.chrome.webview.addEventListener('message', event => {
    const data = event.data;
    
    if (data.Type === "SegmentUpdate") {
        // Update Editor Content
        updateEditorContent(data.Source, data.Target);
        
        // Trigger AI Request (Mock)
        requestAiSuggestion(data.Source, data.Target);
    }
});

// Send "Confirm" signal to C#
function onConfirmTranslation(text) {
    window.chrome.webview.postMessage({
        action: "Inject",
        content: text
    });
}
5. Required Nuget PackagesMemoQ.PreviewInterfaces: (From SDK)MemoQ.Addins.Common: (From SDK)Newtonsoft.Json: For serialization.Microsoft.Web.WebView2: For the embedded browser.