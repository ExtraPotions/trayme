// Copyright (c) 2026 expDARE.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class TrayFixture
{
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [STAThread]
    private static void Main(string[] args)
    {
        var folder = args[0];
        Application.EnableVisualStyles();
        using (var window = new Form { Text = "Terminal Tray Starter test window", Width = 420, Height = 150 })
        using (var timer = new Timer { Interval = 100 })
        {
            window.Controls.Add(new Label { Text = "Disposable test fixture. Closes automatically.", AutoSize = true, Left = 20, Top = 30 });
            var started = DateTime.UtcNow;
            var restored = false;
            timer.Tick += delegate
            {
                if (!restored && File.Exists(Path.Combine(folder, "restore")))
                {
                    restored = true;
                    ShowWindow(window.Handle, 1);
                }
                File.WriteAllText(Path.Combine(folder, "state.txt"), IsWindowVisible(window.Handle) + "," + IsIconic(window.Handle));
                if (File.Exists(Path.Combine(folder, "stop")) || (DateTime.UtcNow - started).TotalSeconds > 40)
                    window.Close();
            };
            window.Shown += delegate { timer.Start(); };
            Application.Run(window);
        }
    }
}
