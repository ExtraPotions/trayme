// Copyright (c) 2026 expDARE.
using System;
using TerminalTrayStarter;

internal static class Tests
{
    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
    }

    private static void Main()
    {
        Check(TerminalPolicy.IsTerminal("ConsoleWindowClass", "unknown"), "Native console not recognized");
        Check(TerminalPolicy.IsTerminal("CASCADIA_HOSTING_WINDOW_CLASS", "unknown"), "Windows Terminal not recognized");
        foreach (var process in new[] { "WindowsTerminal", "mintty", "alacritty", "wezterm-gui", "putty", "tabby", "hyper" })
            Check(TerminalPolicy.IsTerminal("custom", process), "Terminal not recognized: " + process);
        foreach (var process in new[] { "explorer", "T3 Code (Beta)", "OpenCode", "chrome", "pythonw" })
            Check(!TerminalPolicy.IsTerminal("Chrome_WidgetWin_1", process), "Non-terminal selected: " + process);
        var tracker = new WindowTracker();
        var window = new IntPtr(42);
        tracker.Existing(window);
        Check(!tracker.FirstShow(window), "Existing window was selected");
        tracker.Destroyed(window);
        Check(tracker.FirstShow(window), "Reused handle was ignored");
        Check(!tracker.FirstShow(window), "Restored window was selected again");
        Console.WriteLine("17 terminal classification and lifecycle checks passed.");
    }
}
