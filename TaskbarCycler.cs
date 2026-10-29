using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("TaskbarCycler")]
[assembly: AssemblyDescription("Cycles grouped Windows taskbar windows on ordinary left click")]
[assembly: AssemblyProduct("TaskbarCycler")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace TaskbarCyclerApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (HasArg(args, "--uninstall-startup"))
            {
                StartupManager.SetEnabled(false);
                return;
            }

            if (HasArg(args, "--install-startup"))
            {
                StartupManager.SetEnabled(true);
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\TaskbarCycler.Singleton", out createdNew))
            {
                if (!createdNew)
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApplicationContext());
                GC.KeepAlive(mutex);
            }
        }

        private static bool HasArg(string[] args, string wanted)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], wanted, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }

    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _enabledItem;
        private readonly ToolStripMenuItem _startupItem;
        private readonly TaskbarMouseHook _hook;
        private bool _enabled = true;

        public TrayApplicationContext()
        {
            _enabledItem = new ToolStripMenuItem("Переключение окон включено");
            _enabledItem.Checked = true;
            _enabledItem.CheckOnClick = true;
            _enabledItem.Click += delegate
            {
                _enabled = _enabledItem.Checked;
                UpdateTooltip();
            };

            _startupItem = new ToolStripMenuItem("Автозапуск с Windows");
            _startupItem.Checked = StartupManager.IsEnabled();
            _startupItem.CheckOnClick = true;
            _startupItem.Click += delegate
            {
                StartupManager.SetEnabled(_startupItem.Checked);
                _startupItem.Checked = StartupManager.IsEnabled();
            };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Выход");
            exitItem.Click += delegate { ExitApplication(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(_enabledItem);
            menu.Items.Add(_startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            Icon icon = null;
            try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            if (icon == null) icon = SystemIcons.Application;

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = icon;
            _notifyIcon.Visible = true;
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _enabled = !_enabled;
                    _enabledItem.Checked = _enabled;
                    UpdateTooltip();
                }
            };

            UpdateTooltip();

            _hook = new TaskbarMouseHook(delegate { return _enabled; });
            _hook.Start();
        }

        private void UpdateTooltip()
        {
            if (_notifyIcon == null) return;
            _notifyIcon.Text = _enabled
                ? "TaskbarCycler — включен"
                : "TaskbarCycler — выключен";
        }

        private void ExitApplication()
        {
            try { _hook.Dispose(); } catch { }
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _hook.Dispose(); } catch { }
                try
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                catch { }
            }
            base.Dispose(disposing);
        }
    }

    internal static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TaskbarCycler";

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    if (enabled)
                        key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                    else
                        key.DeleteValue(ValueName, false);
                }
            }
            catch { }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    object value = key.GetValue(ValueName);
                    if (value == null) return false;
                    string text = value.ToString().Trim().Trim('"');
                    return string.Equals(text, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }
    }

    internal sealed class TaskbarMouseHook : IDisposable
    {
        private readonly Func<bool> _isEnabled;
        private NativeMethods.LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;
        private bool _armed;
        private bool _injectedCtrl;

        public TaskbarMouseHook(Func<bool> isEnabled)
        {
            _isEnabled = isEnabled;
        }

        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;
            _proc = HookCallback;
            _hookId = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL,
                _proc,
                NativeMethods.GetModuleHandle(null),
                0);

            if (_hookId == IntPtr.Zero)
                throw new InvalidOperationException("Не удалось установить перехватчик мыши.");
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                NativeMethods.MSLLHOOKSTRUCT data = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(
                    lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));

                if ((data.flags & NativeMethods.LLMHF_INJECTED) == 0)
                {
                    if (message == NativeMethods.WM_LBUTTONDOWN)
                    {
                        _armed = false;
                        _injectedCtrl = false;

                        bool enabled = false;
                        try { enabled = _isEnabled(); } catch { }

                        if (enabled && TaskbarDetector.IsApplicationButtonAt(data.pt.x, data.pt.y))
                        {
                            _armed = true;
                            if (!NativeMethods.IsControlDown())
                            {
                                NativeMethods.SendControlKey(true);
                                _injectedCtrl = true;
                            }
                        }
                    }
                    else if (message == NativeMethods.WM_LBUTTONUP)
                    {
                        if (_armed)
                        {
                            if (_injectedCtrl)
                                NativeMethods.SendControlKey(false);

                            _armed = false;
                            _injectedCtrl = false;
                        }
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_injectedCtrl)
            {
                try { NativeMethods.SendControlKey(false); } catch { }
                _injectedCtrl = false;
            }

            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            _proc = null;
        }
    }

    internal static class TaskbarDetector
    {
        public static bool IsApplicationButtonAt(int x, int y)
        {
            try
            {
                AutomationElement element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
                if (element == null) return false;

                bool sawButton = false;
                for (int depth = 0; depth < 14 && element != null; depth++)
                {
                    string automationId = GetString(element, AutomationElement.AutomationIdProperty);
                    string className = GetString(element, AutomationElement.ClassNameProperty);
                    ControlType controlType = GetControlType(element);

                    // Windows 11 app buttons expose an Appid automation id.
                    if (!string.IsNullOrEmpty(automationId) &&
                        automationId.StartsWith("Appid:", StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Current Windows 11 XAML taskbar app button class.
                    if (!string.IsNullOrEmpty(className) &&
                        className.IndexOf("Taskbar.TaskListButton", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;

                    // Explicitly reject the common taskbar controls that are not app buttons.
                    if (IsSpecialTaskbarControl(automationId))
                        return false;

                    if (controlType == ControlType.Button)
                        sawButton = true;

                    // Older taskbar implementation fallback.
                    if (sawButton && string.Equals(className, "MSTaskListWClass", StringComparison.OrdinalIgnoreCase))
                        return true;

                    element = TreeWalker.RawViewWalker.GetParent(element);
                }
            }
            catch
            {
                // If UI Automation is temporarily unavailable, leave the click untouched.
            }

            return false;
        }

        private static bool IsSpecialTaskbarControl(string automationId)
        {
            if (string.IsNullOrEmpty(automationId)) return false;

            string id = automationId.ToLowerInvariant();
            return id == "startbutton" ||
                   id.IndexOf("search") >= 0 ||
                   id.IndexOf("taskview") >= 0 ||
                   id.IndexOf("widgets") >= 0 ||
                   id.IndexOf("copilot") >= 0 ||
                   id.IndexOf("systemtray") >= 0 ||
                   id.IndexOf("notification") >= 0 ||
                   id.IndexOf("showdesktop") >= 0;
        }

        private static string GetString(AutomationElement element, AutomationProperty property)
        {
            try
            {
                object value = element.GetCurrentPropertyValue(property, true);
                if (value == null || value == AutomationElement.NotSupported) return string.Empty;
                return value as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static ControlType GetControlType(AutomationElement element)
        {
            try
            {
                object value = element.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty, true);
                return value as ControlType;
            }
            catch
            {
                return null;
            }
        }
    }

    internal static class NativeMethods
    {
        public const int WH_MOUSE_LL = 14;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const uint LLMHF_INJECTED = 0x00000001;
        private const int VK_CONTROL = 0x11;
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public static bool IsControlDown()
        {
            return (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        }

        public static void SendControlKey(bool down)
        {
            INPUT input = new INPUT();
            input.type = INPUT_KEYBOARD;
            input.U.ki = new KEYBDINPUT();
            input.U.ki.wVk = (ushort)VK_CONTROL;
            input.U.ki.dwFlags = down ? 0u : KEYEVENTF_KEYUP;

            INPUT[] inputs = new INPUT[] { input };
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
