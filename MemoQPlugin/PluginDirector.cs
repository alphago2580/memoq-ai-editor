using System;
using System.Diagnostics;
using MemoQ.Addins.Common.DataStructures;
using MemoQ.Addins.Common.Framework;
using MemoQ.PreviewInterfaces;

namespace MemoQAISidecarPlugin
{
    /// <summary>
    /// Plugin entry point. Registers as a "Fake Preview Tool" to intercept segment events.
    /// </summary>
    public class DummyPreviewToolDirector : IPluginDirector
    {
        private PreviewToolCallback _callback;

        public void Initialize(IModuleEnvironment env)
        {
            try
            {
                // Register as Preview Tool
                var registration = new RegistrationRequest
                {
                    PreviewToolId = new Guid("A1B2C3D4-E5F6-7890-ABCD-1234567890AB"), // Unique GUID
                    PreviewToolName = "AI Sidecar Assistant",
                    ContentComplexity = ContentComplexity.Minimal, // Plain text
                    AutoStartupCommand = ""
                };

                _callback = new PreviewToolCallback();
                env.PreviewEnvironment.RegisterPreviewTool(registration, _callback);

                Debug.WriteLine("[MemoQ AI Sidecar] Plugin registered successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoQ AI Sidecar] Initialization failed: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            _callback?.Dispose();
        }

        public string GetHelpText() => "AI-powered translation assistant with Ghost Text autocompletion.";
        public string GetDisplayName() => "AI Sidecar";
        public string GetDescription() => "External editor with live AI suggestions";
        public PluginFeatureType GetFeatureType() => PluginFeatureType.PreviewTool;
    }
}
