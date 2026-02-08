using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

namespace SidecarApp
{
    /// <summary>
    /// Injects translated text back into MemoQ using WinAPI and SendKeys.
    /// </summary>
    public class KeyboardInjector
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public void InjectTranslationToMemoQ(string translation)
        {
            try
            {
                // 1. Copy to Clipboard
                Clipboard.SetText(translation);

                // 2. Find MemoQ process
                var memoqProc = Process.GetProcessesByName("MemoQ").FirstOrDefault();
                if (memoqProc == null)
                {
                    System.Diagnostics.Debug.WriteLine("[Injector] MemoQ process not found.");
                    return;
                }

                // 3. Activate MemoQ window
                SetForegroundWindow(memoqProc.MainWindowHandle);
                Thread.Sleep(100); // Wait for focus switch

                // 4. Send keyboard macros
                SendKeys.SendWait("^a");      // Ctrl+A (Select All)
                Thread.Sleep(50);
                SendKeys.SendWait("{DEL}");   // Delete
                Thread.Sleep(50);
                SendKeys.SendWait("^v");      // Ctrl+V (Paste)
                Thread.Sleep(100);
                SendKeys.SendWait("^{ENTER}"); // Ctrl+Enter (Confirm & Next Segment)

                System.Diagnostics.Debug.WriteLine("[Injector] Translation injected successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Injector] Failed: {ex.Message}");
            }
        }
    }
}
