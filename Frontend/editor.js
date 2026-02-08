import { EditorView, basicSetup } from "codemirror";
import { EditorState, StateEffect, StateField } from "@codemirror/state";
import { Decoration, keymap } from "@codemirror/view";

// ===== State Management =====
let currentSourceText = "";
let currentTargetText = "";
let currentGhostText = "";
let aiRequestTimeout = null;

// ===== Ghost Text State Field =====
const addGhostText = StateEffect.define();
const clearGhostText = StateEffect.define();

const ghostTextField = StateField.define({
    create() {
        return Decoration.none;
    },
    update(decorations, tr) {
        decorations = decorations.map(tr.changes);

        for (let effect of tr.effects) {
            if (effect.is(addGhostText)) {
                const { pos, text } = effect.value;
                const widget = Decoration.widget({
                    widget: new GhostTextWidget(text),
                    side: 1
                });
                decorations = Decoration.set([widget.range(pos)]);
            } else if (effect.is(clearGhostText)) {
                decorations = Decoration.none;
            }
        }
        return decorations;
    },
    provide: f => EditorView.decorations.from(f)
});

class GhostTextWidget {
    constructor(text) {
        this.text = text;
    }

    toDOM() {
        const span = document.createElement("span");
        span.className = "ghost-text-widget";
        span.textContent = this.text;
        return span;
    }

    ignoreEvent() {
        return true;
    }
}

// ===== Accept Ghost Text on Tab =====
const acceptGhostKeymap = keymap.of([
    {
        key: "Tab",
        run: (view) => {
            if (currentGhostText) {
                const pos = view.state.selection.main.head;
                view.dispatch({
                    changes: { from: pos, insert: currentGhostText },
                    effects: clearGhostText.of(null)
                });
                currentGhostText = "";
                return true;
            }
            return false;
        }
    },
    {
        key: "Ctrl-Enter",
        run: (view) => {
            confirmTranslation(view.state.doc.toString());
            return true;
        }
    }
]);

// ===== AI Request Handler =====
function requestAISuggestion(view) {
    const doc = view.state.doc.toString();
    const cursor = view.state.selection.main.head;

    // Clear previous timeout
    if (aiRequestTimeout) {
        clearTimeout(aiRequestTimeout);
    }

    // Debounce: wait 500ms after user stops typing
    aiRequestTimeout = setTimeout(async () => {
        try {
            showSpinner(true);
            updateStatus("Generating suggestion...");

            const suggestion = await callAI(currentSourceText, doc);

            if (suggestion && suggestion.trim()) {
                currentGhostText = suggestion;
                view.dispatch({
                    effects: addGhostText.of({ pos: cursor, text: suggestion })
                });
                updateStatus("Suggestion ready (Tab to accept)");
            } else {
                updateStatus("Ready");
            }
        } catch (error) {
            console.error("AI request failed:", error);
            updateStatus("AI request failed");
        } finally {
            showSpinner(false);
        }
    }, 500);
}

// ===== Mock AI Function (Replace with real Ollama API) =====
async function callAI(sourceText, currentTranslation) {
    // TODO: Replace with actual Ollama API call
    // Example endpoint: http://localhost:11434/api/generate

    // Mock delay
    await new Promise(resolve => setTimeout(resolve, 800));

    // Mock suggestion based on source text
    if (!currentTranslation || currentTranslation.trim().length < 5) {
        // Provide initial suggestion
        return `[AI Suggestion for: "${sourceText}"]`;
    }

    // Provide continuation suggestion
    const words = currentTranslation.trim().split(/\s+/);
    if (words.length < 3) {
        return " continued text here";
    }

    return ""; // No suggestion
}

// ===== Real Ollama Integration (Uncomment when ready) =====
/*
async function callAI(sourceText, currentTranslation) {
    const response = await fetch("http://localhost:11434/api/generate", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            model: "llama2", // or your preferred model
            prompt: `Translate the following text from source to target language. Provide only the translation, no explanations.

Source: ${sourceText}
Current Translation: ${currentTranslation}

Continue or complete the translation:`,
            stream: false
        })
    });

    const data = await response.json();
    return data.response?.trim() || "";
}
*/

// ===== Editor Initialization =====
const startState = EditorState.create({
    doc: "Start typing your translation...",
    extensions: [
        basicSetup,
        ghostTextField,
        acceptGhostKeymap,
        EditorView.updateListener.of(update => {
            if (update.docChanged) {
                // Clear ghost text when user types
                if (currentGhostText) {
                    update.view.dispatch({
                        effects: clearGhostText.of(null)
                    });
                    currentGhostText = "";
                }

                // Request new AI suggestion
                requestAISuggestion(update.view);
            }
        }),
        EditorView.theme({
            "&": {
                height: "100%",
                backgroundColor: "#1e1e1e"
            },
            ".cm-scroller": {
                fontFamily: "'Fira Code', 'Consolas', monospace"
            }
        })
    ]
});

const editorView = new EditorView({
    state: startState,
    parent: document.getElementById("editor")
});

// ===== WebView2 Communication =====
function updateSourceText(text) {
    currentSourceText = text;
    document.getElementById("sourceText").textContent = text;
}

function updateEditorContent(targetText) {
    currentTargetText = targetText;
    editorView.dispatch({
        changes: {
            from: 0,
            to: editorView.state.doc.length,
            insert: targetText || ""
        }
    });
}

function confirmTranslation(translation) {
    // Send translation to C# WPF via WebView2 message
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({
            action: "Inject",
            content: translation
        });
        updateStatus("Translation sent to MemoQ!");
    } else {
        console.log("WebView2 not available. Translation:", translation);
    }
}

// ===== Listen for messages from C# =====
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', event => {
        const data = event.data;

        if (typeof data === 'string') {
            try {
                const parsed = JSON.parse(data);
                handleSegmentUpdate(parsed);
            } catch {
                console.error("Failed to parse message:", data);
            }
        } else {
            handleSegmentUpdate(data);
        }
    });
}

function handleSegmentUpdate(data) {
    if (data.Type === "SegmentUpdate") {
        updateSourceText(data.Source || "");
        updateEditorContent(data.Target || "");
        updateStatus(`Loaded: ${data.SourceLang} → ${data.TargetLang}`);
    }
}

// ===== UI Helpers =====
function updateStatus(text) {
    document.getElementById("statusText").textContent = text;
}

function showSpinner(show) {
    const spinner = document.getElementById("spinner");
    if (show) {
        spinner.classList.add("active");
    } else {
        spinner.classList.remove("active");
    }
}

// ===== Export for debugging =====
window.debugEditor = {
    getContent: () => editorView.state.doc.toString(),
    setContent: (text) => updateEditorContent(text),
    setSource: (text) => updateSourceText(text),
    injectGhost: (text) => {
        const pos = editorView.state.selection.main.head;
        currentGhostText = text;
        editorView.dispatch({
            effects: addGhostText.of({ pos, text })
        });
    }
};

console.log("MemoQ AI Sidecar Editor initialized.");
console.log("Debug API available: window.debugEditor");
