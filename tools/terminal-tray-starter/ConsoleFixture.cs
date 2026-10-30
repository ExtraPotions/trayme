// Copyright (c) 2026 expDARE.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class ConsoleFixture
{
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();
    [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int length);
    private static void Main(string[] args)
    {
        var folder = args[0];
        if (!AllocConsole()) throw new InvalidOperationException("Disposable console allocation failed.");
        Console.Title = "Terminal Tray Starter disposable console";
        var window = GetConsoleWindow();
        var name = new StringBuilder(256);
        GetClassName(window, name, name.Capacity);
        if (name.ToString() != "ConsoleWindowClass")
            throw new InvalidOperationException("Fixture did not receive a real classic console window.");
        File.WriteAllText(Path.Combine(folder, "console-class.txt"), name.ToString());
        var started = DateTime.UtcNow;
        var restored = false;
        while (!File.Exists(Path.Combine(folder, "stop")) && (DateTime.UtcNow - started).TotalSeconds < 40)
        {
            if (!restored && File.Exists(Path.Combine(folder, "restore")))
            {
                restored = true;
                ShowWindow(window, 1);
            }
            File.WriteAllText(Path.Combine(folder, "state.txt"), IsWindowVisible(window) + "," + IsIconic(window));
            Thread.Sleep(100);
        }
    }
}
