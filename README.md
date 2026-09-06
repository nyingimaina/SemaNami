# SemaNami

A small, general-purpose way to send yourself a Telegram message from any tool or script —
a build finishing, a long job completing, a deploy landing, anything.

```
SemaNami -sender "Build Script" -message "Build finished successfully"
```

Cross-platform (Windows, macOS, Linux). Windows ships a proper installer that puts `SemaNami`
on your PATH and walks you through setup; macOS/Linux get a self-contained binary + install
script.

Two pieces:
- **`SemaNami.Core`** — a class library any .NET app/tool can reference directly.
- **`SemaNami.Cli`** — the `SemaNami` console command any script (PowerShell, bash, a CI step,
  a non-.NET tool) can invoke as a plain process.

## Install

### Windows

Run `dist/installer/SemaNamiSetup.exe` (build it first — see below). It's a per-user install,
no admin required, adds `SemaNami` to your PATH, and offers to run setup immediately after
install.

### macOS / Linux

Download/copy the `dist/<rid>/` folder for your platform (`osx-x64`, `osx-arm64`, or
`linux-x64`) — it contains the `SemaNami` binary and `install.sh`. Then:

```bash
chmod +x install.sh SemaNami
./install.sh
```

This copies `SemaNami` to `/usr/local/bin` (already on PATH by default on macOS and most Linux
distributions) and runs `SemaNami --setup` for you.

## Setup

Run `SemaNami --setup` (the installer offers to do this automatically). It walks you through
everything that's possible to automate:

1. **The one thing that can't be automated**: Telegram requires a human to create a bot.
   The wizard tells you to open **@BotFather** in Telegram, send `/newbot`, and paste the
   token it gives you back into the wizard.
2. Once you paste the token, the wizard validates it, tells you your bot's `@username`, and
   asks you to message that bot once (so it's allowed to message you back).
3. Everything after that is automatic: it detects your chat id, saves both values (Windows
   user environment variables, or a sourced env file on macOS/Linux), and sends you a live
   confirmation message.

Re-running `SemaNami --setup` when already configured just confirms that and exits — safe to
run again any time.

## Usage

```
SemaNami -sender "<Sender Name>" -message "<Chat Message>"
SemaNami --setup
SemaNami --get-chat-id
SemaNami --help
```

Running `SemaNami` with no arguments prints the same usage to stdout.

Exit code is `0` on success, `1` on failure (not configured yet, bad arguments, send failure)
— safe to check from a script (`if ($LASTEXITCODE -ne 0) { ... }` / `if [ $? -ne 0 ]; then ...`).

### From another .NET project directly

Reference `SemaNami.Core` and use `Notifier` — no process-spawn overhead:

```csharp
var sender = new TelegramBotMessageSender(botToken);
var notifier = new Notifier(sender, chatId);
await notifier.NotifyAsync("Deploy complete");
```

## Building

```powershell
dotnet test                    # run the test suite
pwsh scripts/build-all.ps1     # publish self-contained binaries for all platforms into dist/
```

To also build the Windows installer (after `build-all.ps1` has produced `dist/win-x64/`):

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\SemaNami.iss
```

Produces `dist/installer/SemaNamiSetup.exe`.
