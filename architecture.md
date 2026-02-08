🚀 Project Blueprint: MemoQ AI Sidecar Editor (Async Event-Driven Edition)1. 프로젝트 개요 (Overview)이 프로젝트는 MemoQ의 폐쇄적인 에디터 환경을 벗어나, **외부 편집기(Sidecar)**에서 **AI 기반 자동완성(Ghost Text)**과 현대적인 UX를 제공하는 하이브리드 번역 도구를 구축합니다.핵심 철학 & 변경 사항Event-Driven: MT SDK 대신 Preview SDK를 메인으로 사용하여 MemoQ의 커서 이동 이벤트를 비동기로 감지합니다. (MemoQ 성능 저하 0%)Sidecar Editor: WPF + WebView2 기반의 외부 창을 MemoQ 위에 오버레이(Overlay)하거나 별도 창으로 띄워 편집 환경을 제공합니다.Macro Injection: 번역이 완료되면 OS 레벨의 키보드 매크로를 통해 MemoQ에 결과물을 주입합니다.2. 시스템 아키텍처 (Architecture)📐 데이터 흐름도 (Data Flow)sequenceDiagram
    participant MemoQ as MemoQ Client
    participant Plugin as Preview Plugin (Fake)
    participant Sidecar as Sidecar App (C# WPF)
    participant UI as WebView2 (CodeMirror)
    participant AI as Local LLM (Ollama)

    Note over MemoQ, Plugin: 1. 감지 (Listening)
    MemoQ->>Plugin: ChangeHighlightRequest (Cursor Moved)
    Plugin->>Sidecar: 데이터 전송 (Named Pipe / REST)
    Plugin-->>MemoQ: OK (200) - 즉시 응답

    Note over Sidecar, UI: 2. 편집 & AI (Editing)
    Sidecar->>UI: setContent(source, target)
    loop Typing
        UI->>AI: 프롬프트 전송 (Context + Glossary)
        AI-->>UI: Ghost Text 제안
        User->>UI: Tab으로 수락 / 편집
    end

    Note over Sidecar, MemoQ: 3. 주입 (Injection)
    User->>UI: Ctrl+Enter (완료 신호)
    UI->>Sidecar: 최종 번역문 전송
    Sidecar->>MemoQ: 1. WinAPI: SetForegroundWindow(MemoQ)
    Sidecar->>MemoQ: 2. SendKeys: Ctrl+A (Select All) -> Del
    Sidecar->>MemoQ: 3. SendKeys: Ctrl+V (Paste Translation)
    Sidecar->>MemoQ: 4. SendKeys: Ctrl+Enter (Confirm & Next)
3. 기술 스택 (Tech Stack)A. MemoQ Plugin (Listener)Role: "가짜 미리보기 도구"로 위장하여 실시간 데이터 수신.SDK: Preview SDK (MemoQ.PreviewInterfaces).Communication: Named Pipe Client 또는 REST Client.Logic: IPreviewToolCallback.HandleChangeHighlightRequest 구현.B. Sidecar App (Host)Framework: .NET 6+ (WPF) 또는 .NET Framework 4.8.Core: Microsoft.Web.WebView2 (Chromium).Role:투명 창 관리 (Window Style: None, AllowsTransparency: True).글로벌 핫키 감지 (MemoQ가 활성화된 상태에서 Sidecar 호출).WinAPI 제어: user32.dll을 이용한 창 활성화 및 키 입력 전송.C. Frontend (Editor UI)Core: CodeMirror 6.Features:InlineCompletion (Ghost Text).StreamLanguage (Syntax Highlighting).Keymap (Custom Shortcuts).Styling: CSS Variables 기반의 다크 테마.4. 상세 구현 가이드 (Implementation Details)Phase 1: MemoQ Plugin (Preview SDK 활용)목표: MemoQ가 "미리보기 업데이트해라"라고 보낸 데이터를 낚아채서 Sidecar로 던진다.등록: RegistrationRequest를 통해 자신을 "AI Sidecar Preview"로 등록.수신:// IPreviewToolCallback 구현
public void HandleChangeHighlightRequest(ChangeHighlightRequestFromMQ request)
{
    // 1. 필요한 데이터 추출
    var payload = new SegmentData {
        Source = request.ActivePreviewParts[0].SourceContent.Content,
        Target = request.ActivePreviewParts[0].TargetContent.Content,
        // PreviewPartId 등 식별자 저장
    };

    // 2. Sidecar로 비동기 전송 (Fire-and-forget)
    _pipeClient.SendAsync(payload);
}
Phase 2: Sidecar App (Injection Logic)목표: WebView2에서 완료 신호가 오면 MemoQ에 텍스트를 때려 박는다.[DllImport("user32.dll")]
static extern bool SetForegroundWindow(IntPtr hWnd);

public void InjectAndNext(string translation)
{
    // 1. 클립보드에 번역문 저장
    Clipboard.SetText(translation);

    // 2. MemoQ 창 찾기 & 활성화
    Process memoq = Process.GetProcessesByName("MemoQ").FirstOrDefault();
    if (memoq != null)
    {
        SetForegroundWindow(memoq.MainWindowHandle);
        Thread.Sleep(50); // 포커스 전환 대기

        // 3. 키 매크로 전송 (기존 내용 삭제 -> 붙여넣기 -> 확정)
        SendKeys.SendWait("^a");      // Ctrl+A
        SendKeys.SendWait("{DEL}");   // Delete
        SendKeys.SendWait("^v");      // Ctrl+V
        Thread.Sleep(50);
        SendKeys.SendWait("^{ENTER}"); // Ctrl+Enter (Confirm & Next)
    }
}
Phase 3: Frontend (CodeMirror 6 & Ghost Text)목표: AI가 제안한 텍스트를 에디터 안에 회색으로 렌더링한다.Ghost Text 구현 전략:CodeMirror 6의 ViewPlugin 또는 StateField를 사용하여 Decoration을 관리.사용자 입력(updateListener) -> Debounce(300ms) -> AI 요청.AI 응답 수신 -> 현재 커서 뒤에 Decoration.widget (type: widget) 삽입.Tab 키 입력 시: Ghost Text 내용을 실제 문서(state.doc)에 삽입하고 Decoration 제거.5. 디자인 시스템 (CSS for AI Sidecar)Claude가 디자인에 약하므로, 아래 CSS를 그대로 적용하도록 지시하십시오. "Cyberpunk meets VS Code" 컨셉의 고대비 다크 테마입니다.:root {
    /* Base Colors */
    --bg-main: #1e1e1e;       /* VS Code Default Bg */
    --bg-panel: #252526;      /* Side Panel Bg */
    --bg-input: #3c3c3c;      /* Input Field Bg */
    
    /* Text Colors */
    --text-main: #cccccc;
    --text-muted: #858585;
    --text-ghost: #6a9955;    /* AI 제안 텍스트 (약간 초록빛 도는 회색) */
    
    /* Accents */
    --accent-primary: #007acc; /* VS Code Blue */
    --accent-hover: #0098ff;
    --border-color: #454545;
    
    /* Typography */
    --font-code: 'Fira Code', 'Consolas', monospace;
    --font-ui: 'Segoe UI', sans-serif;
}

body {
    margin: 0;
    background-color: var(--bg-main);
    color: var(--text-main);
    font-family: var(--font-ui);
    overflow: hidden;
    height: 100vh;
    display: flex;
    flex-direction: column;
}

/* 1. Header (Source Text Area) */
.header-panel {
    padding: 12px 16px;
    background-color: var(--bg-panel);
    border-bottom: 1px solid var(--border-color);
    box-shadow: 0 2px 4px rgba(0,0,0,0.2);
}

.source-label {
    font-size: 0.75rem;
    color: var(--text-muted);
    text-transform: uppercase;
    letter-spacing: 0.5px;
    margin-bottom: 4px;
}

.source-content {
    font-size: 1rem;
    line-height: 1.5;
    font-weight: 500;
}

/* 2. Editor Container */
.editor-wrapper {
    flex: 1;
    position: relative;
    background-color: var(--bg-main);
}

/* CodeMirror Overrides */
.cm-editor {
    height: 100%;
    font-family: var(--font-code) !important;
    font-size: 16px;
}

.cm-scroller {
    padding: 10px 0;
}

.cm-content {
    caret-color: var(--accent-primary);
}

/* GHOST TEXT STYLE (핵심) */
.ghost-text-widget {
    color: var(--text-ghost);
    opacity: 0.8;
    font-style: italic;
    pointer-events: none; /* 클릭 방지 */
}

/* 3. Status Bar & Actions */
.status-bar {
    height: 28px;
    background-color: var(--accent-primary);
    color: white;
    display: flex;
    align-items: center;
    padding: 0 12px;
    font-size: 0.8rem;
    justify-content: space-between;
}

.status-item {
    display: flex;
    align-items: center;
    gap: 6px;
}

/* Loading Indicator */
.spinner {
    width: 10px;
    height: 10px;
    border: 2px solid rgba(255,255,255,0.3);
    border-top-color: white;
    border-radius: 50%;
    animation: spin 1s linear infinite;
    display: none; /* JS로 제어 */
}

@keyframes spin { to { transform: rotate(360deg); } }
6. 개발 단계별 체크리스트 (Roadmap)Step 1: Sidecar SkeletonWPF 프로젝트 생성 및 WebView2 컨트롤 배치.index.html 로드 테스트.WinAPI SetForegroundWindow 작동 테스트 (메모장 켜두고 해보기).Step 2: MemoQ ListenerPreview SDK 샘플 코드(DummyPreviewTool)를 빌드하여 DLL 생성.MemoQ 옵션에서 해당 Preview Tool 활성화.커서 이동 시 로그가 찍히는지 확인.Step 3: Frontend AICodeMirror 6 기본 에디터 구현.더미 AI 함수(setTimeout으로 흉내) 만들어서 Ghost Text 렌더링 테스트.Tab 키로 수락하는 로직 구현.Step 4: IntegrationMemoQ Plugin -> Named Pipe -> WPF -> WebView2 데이터 흐름 연결.WebView2 Ctrl+Enter -> WPF -> WinAPI -> MemoQ 주입 흐름 연결.7. 참고 문서 (Reference)Preview SDK: IPreviewToolCallback, ChangeHighlightRequestWinAPI: user32.dll (SetForegroundWindow, SendKeys)CodeMirror 6: Decoration, ViewPlugin, EditorView.updateListener