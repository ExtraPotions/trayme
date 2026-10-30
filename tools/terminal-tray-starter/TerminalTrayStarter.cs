// Copyright (c) 2026 expDARE.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TerminalTrayStarter
{
    internal static class TerminalPolicy
    {
        internal static bool IsTerminal(string className, string processName)
        {
            if (String.Equals(className, "ConsoleWindowClass", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(className, "CASCADIA_HOSTING_WINDOW_CLASS", StringComparison.OrdinalIgnoreCase))
                return true;
            switch ((processName ?? "").ToLowerInvariant())
            {
                case "windowsterminal": case "mintty": case "alacritty":
                case "wezterm-gui": case "putty": case "tabby": case "hyper":
                    return true;
                default: return false;
            }
        }
    }

    internal sealed class WindowTracker
    {
        private readonly HashSet<IntPtr> seen = new HashSet<IntPtr>();
        internal void Existing(IntPtr window) { seen.Add(window); }
        internal void Destroyed(IntPtr window) { seen.Remove(window); }
        internal bool FirstShow(IntPtr window) { return seen.Add(window); }
    }

    internal static class Native
    {
        internal delegate void WinEventProc(IntPtr hook, uint evt, IntPtr window,
            int objectId, int childId, uint thread, uint time);
        internal delegate bool EnumProc(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint min, uint max,
            IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
        [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(IntPtr window, StringBuilder name, int length);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr window, int command);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    }

    internal sealed class StarterContext : ApplicationContext
    {
        private readonly WindowTracker tracker = new WindowTracker();
        private readonly Queue<IntPtr> pending = new Queue<IntPtr>();
        private readonly Dictionary<IntPtr, NotifyIcon> hidden = new Dictionary<IntPtr, NotifyIcon>();
        private readonly Native.WinEventProc callback;
        private readonly System.Windows.Forms.Timer dispatch;
        private readonly NotifyIcon icon;
        private readonly Control control;
        private readonly EventWaitHandle stop;
        private readonly RegisteredWaitHandle stopWait;
        private IntPtr hook;
        private bool paused;

        internal StarterContext()
        {
            // Ignore windows already open, including ones hidden by another tray utility.
            Native.EnumWindows(delegate(IntPtr window, IntPtr unused) { tracker.Existing(window); return true; }, IntPtr.Zero);
            callback = OnWindowEvent;
            hook = Native.SetWinEventHook(0x8001, 0x8002, IntPtr.Zero, callback, 0, 0, 2);
            if (hook == IntPtr.Zero) throw new InvalidOperationException("Could not watch new windows.");
            dispatch = new System.Windows.Forms.Timer { Interval = 30 };
            dispatch.Tick += Dispatch;
            var menu = new ContextMenuStrip();
            var pause = new ToolStripMenuItem("Pause new terminals");
            pause.Click += delegate { paused = !paused; pause.Text = paused ? "Resume new terminals" : "Pause new terminals"; };
            menu.Items.Add(pause);
            menu.Items.Add("Restore all terminals", null, delegate { RestoreAll(); });
            menu.Items.Add("Exit", null, delegate { ExitThread(); });
            icon = new NotifyIcon { Icon = SystemIcons.Application,
                Text = "Terminal Tray Starter", ContextMenuStrip = menu, Visible = true };
            control = new Control();
            var handle = control.Handle;
            stop = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\ExtraPotions.TerminalTrayStarter.Stop");
            stopWait = ThreadPool.RegisterWaitForSingleObject(stop, delegate
            {
                try { control.BeginInvoke((MethodInvoker)ExitThread); }
                catch (InvalidOperationException) { }
            }, null, Timeout.Infinite, true);
        }

        private void RemoveIcon(IntPtr window)
        {
            NotifyIcon item;
            if (!hidden.TryGetValue(window, out item)) return;
            hidden.Remove(window);
            item.Visible = false;
            item.ContextMenuStrip.Dispose();
            item.Dispose();
        }

        private void Restore(IntPtr window)
        {
            RemoveIcon(window);
            if (!Native.IsWindow(window)) return;
            Native.ShowWindowAsync(window, 9);
            Native.SetForegroundWindow(window);
        }

        private void RestoreAll()
        {
            foreach (var window in new List<IntPtr>(hidden.Keys)) Restore(window);
        }

        private void OnWindowEvent(IntPtr source, uint evt, IntPtr window,
            int objectId, int childId, uint thread, uint time)
        {
            if (window == IntPtr.Zero || objectId != 0 || childId != 0) return;
            if (evt == 0x8001) { tracker.Destroyed(window); RemoveIcon(window); return; }
            if (!Native.IsWindowVisible(window) || Native.GetAncestor(window, 2) != window) return;
            if (hidden.ContainsKey(window)) { RemoveIcon(window); return; }
            if (!tracker.FirstShow(window) || paused) return;
            pending.Enqueue(window);
            dispatch.Start();
        }

        private void Dispatch(object sender, EventArgs args)
        {
            dispatch.Stop();
            while (pending.Count > 0)
            {
                var window = pending.Dequeue();
                if (paused || !Native.IsWindow(window) || !Native.IsWindowVisible(window)) continue;
                try
                {
                    var name = new StringBuilder(256);
                    Native.GetClassName(window, name, name.Capacity);
                    uint pid;
                    Native.GetWindowThreadProcessId(window, out pid);
                    string processName;
                    using (var process = Process.GetProcessById((int)pid))
                    {
                        processName = process.ProcessName;
                        if (!TerminalPolicy.IsTerminal(name.ToString(), processName)) continue;
                    }
                    var menu = new ContextMenuStrip();
                    menu.Items.Add("Restore", null, delegate { Restore(window); });
                    var item = new NotifyIcon { Icon = SystemIcons.Application,
                        Text = ("Terminal: " + processName).Substring(0, Math.Min(63, 10 + processName.Length)),
                        ContextMenuStrip = menu, Visible = true };
                    item.MouseClick += delegate(object source, MouseEventArgs click)
                    {
                        if (click.Button == MouseButtons.Left) Restore(window);
                    };
                    hidden.Add(window, item);
                    if (!Native.ShowWindowAsync(window, 0)) RemoveIcon(window);
                }
                catch (ArgumentException) { } // Window/process exited between the event and dispatch.
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }

        protected override void ExitThreadCore()
        {
            if (hook != IntPtr.Zero) { Native.UnhookWinEvent(hook); hook = IntPtr.Zero; }
            dispatch.Stop();
            dispatch.Dispose();
            stopWait.Unregister(null);
            stop.Dispose();
            RestoreAll();
            control.Dispose();
            icon.Visible = false;
            icon.ContextMenuStrip.Dispose();
            icon.Dispose();
            base.ExitThreadCore();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--stop")
            {
                try
                {
                    using (var stop = EventWaitHandle.OpenExisting("Local\\ExtraPotions.TerminalTrayStarter.Stop")) stop.Set();
                }
                catch (WaitHandleCannotBeOpenedException) { }
                return 0;
            }
            if (args.Length != 0) return 2;
            bool created;
            using (var mutex = new Mutex(true, "Local\\ExtraPotions.TerminalTrayStarter", out created))
            {
                if (!created) return 0;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { Application.Run(new StarterContext()); }
                catch (Exception error)
                {
                    MessageBox.Show(error.Message, "Terminal Tray Starter", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }
            return 0;
        }
    }
}
