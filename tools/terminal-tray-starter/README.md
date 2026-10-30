# Terminal Tray Starter

Copyright (c) 2026 expDARE.

A small Windows helper that sends newly opened standalone terminal windows directly to tray icons. It is independent of Trayme and contains no Trayme code. Trayme can continue managing T3 Code and OpenCode.

## Behavior

- Native console windows are recognized by their window class, covering PowerShell, Command Prompt and other console programs without maintaining an executable list.
- Windows Terminal, Git Bash/mintty, Alacritty, WezTerm, PuTTY, Tabby and Hyper are also recognized.
- Existing windows are left alone. A restored window is not automatically hidden again.
- Click a terminal's tray icon to restore it. Windows may place icons in the notification overflow menu.
- The helper's own tray menu offers Pause new terminals, Restore all terminals, and Exit. Exit restores every window hidden by the helper.
- `TerminalTrayStarter.exe --stop` requests a clean exit, including restoration.

The helper uses out-of-process Windows show/destroy events. It does not inject code into other programs, read terminal contents, execute shell commands, or continuously poll windows. A one-shot 30ms dispatch delay keeps work out of the event callback.

## Build and install

Run `./build.ps1` from PowerShell. It uses Windows' .NET Framework compiler and needs no downloaded packages. Run `./install.ps1` to copy the tested executable into the current user's Programs folder, register startup and launch it.

Startup can be removed through Windows Startup Apps or by removing only the `TerminalTrayStarter` value under the current user's Run key. Exit the helper through its tray menu before removing its folder.

## Validation

The build runs 17 classification/lifecycle checks. `./smoke-test.ps1` uses a disposable terminal-emulator fixture. `./smoke-test.ps1 -Fixture ConsoleFixture.exe` uses a real native console. Both require the helper to be running and confirm hide, restore, and no repeated hiding. `-Minimized` checks an initially minimized window. `-StopHelper` checks that clean helper shutdown restores hidden windows.

Fixtures have a 40-second lifetime limit, and the smoke test signals stop and awaits cleanup in `finally`.

## Limits

A brief startup flash can occur because Windows must create a window before its show event can be handled. Unknown custom terminal emulators may need their process name added. Embedded terminal panels inside T3 Code, OpenCode and editors are not standalone windows and are not affected. The helper runs without elevation; Windows access restrictions may prevent it from hiding elevated terminal windows. Abrupt process termination or a crash can leave windows hidden; use the helper's Exit command or `--stop` for a normal shutdown. Full Windows sign-out ends the underlying programs as usual.

