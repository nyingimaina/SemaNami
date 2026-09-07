# SemaNami

A small, general-purpose way to open a line of communication from any tool or script to
yourself on Telegram — not just one-way notifications, but full two-way conversations: send a
message, get a reply back seconds or days later, from any process, across any number of turns.

```
SemaNami -sender "Build Script" -message "Build finished successfully"
```

Cross-platform (Windows, macOS, Linux). Windows ships a proper installer that puts `SemaNami`
on your PATH and walks you through setup; macOS/Linux get a self-contained binary + install
script. Both also register a small background listener that runs continuously (Task Scheduler
on Windows, `launchd` on macOS, `systemd --user` on Linux) so replies are captured even when
nothing else is running.

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

Running `SemaNami` with no arguments prints a full mode-by-mode usage summary to stdout — the
same content as this section, kept in sync in `CliArgs.Usage`. Every mode below is independent;
pick the one that matches what you're actually trying to do.

### One-way notification — no reply expected

```
SemaNami -sender "<Sender Name>" -message "<Chat Message>"
```

Fires a message and exits immediately. Use this for build/deploy/job completion pings where
you never need to read a reply back. Unchanged from SemaNami's original behavior.

### Multi-turn conversation, async — reply may come back seconds or days later

```
SemaNami -sender "<Sender Name>" -message "<Chat Message>" -conversation <id>
```

Adding `-conversation <id>` starts (or continues) a named thread and returns immediately,
exactly like the one-way form — it never blocks waiting for a reply. `<id>` is scoped to your
`-sender` name, so two different tools can each use `-conversation deploy` without colliding.
Use this whenever the reply might not come for a while and you (or another process, possibly
started later) will check back for it.

```
SemaNami -sender "Deploy" -message "Ship it?" -conversation deploy-42
```

### Reading a conversation — check for replies at any later time, from any process

```
SemaNami --conversation-history <id> -sender "<Sender Name>" [-after <seq>]
```

Prints the conversation's messages as JSON to stdout and exits — safe to call repeatedly as a
poll. `-after <seq>` (the `seq` from a previously-seen message) returns only newer messages.
Use this for the "any process, any later time" half of async conversations: it never blocks.

### Closing a conversation

```
SemaNami --close-conversation <id> -sender "<Sender Name>"
```

Marks a conversation closed once you're done with it. SemaNami falls back to guessing which
open conversation an un-threaded reply belongs to, so closing conversations you're finished
with keeps that guess reliable for everyone using SemaNami on the machine, not just you.

### Realtime wait — block until the next reply, or time out

```
SemaNami --wait-for-reply -sender "<Sender Name>" -conversation <id> [-after <seq>] [--timeout <seconds>]
```

Blocks and returns as soon as a new message exists (near-instant if the background listener is
running), or times out (`--timeout`, default 300s) and exits with code `2` if nothing arrives.
Use this only when you're actively waiting right now for a specific reply — not for the
arbitrary-delay case, which is what `-conversation` + `--conversation-history` is for. If the
background listener isn't reachable, this transparently falls back to polling — it never
hard-fails just because the fast path is unavailable.

### One-time setup and chat id discovery

```
SemaNami --setup
SemaNami --get-chat-id
```

See [Setup](#setup) above.

### Service management — normally automatic, only needed to fix a broken registration

```
SemaNami --install-service
SemaNami --uninstall-service
```

The installer (Windows) and `install.sh` (macOS/Linux) already run `--install-service` for you
right after `--setup`. Run it by hand only if the background listener somehow isn't registered
(e.g. after a manual binary swap) or you want to remove it (`--uninstall-service`).

### Background listener — not run directly

```
SemaNami --listen
```

The long-running process that continuously polls Telegram for replies and persists them, so
they're there whenever `--conversation-history` or `--wait-for-reply` checks. Normally started
for you as a per-user background service by `--install-service`; run directly only if you're
debugging the listener itself. Only one instance runs at a time — a second `--listen` (or a
service-managed one already running) exits immediately rather than double-processing updates.

### Help

```
SemaNami --help
```

Exit code is `0` on success, `1` on failure (not configured yet, bad arguments, send failure),
and `2` specifically for `--wait-for-reply` timing out with nothing new — safe to check from a
script (`if ($LASTEXITCODE -ne 0) { ... }` / `if [ $? -ne 0 ]; then ...`).

### From another .NET project directly

Reference `SemaNami.Core` and use `Notifier` for one-way notifications — no process-spawn
overhead:

```csharp
var sender = new TelegramBotMessageSender(botToken);
var notifier = new Notifier(sender, chatId);
await notifier.NotifyAsync("Deploy complete");
```

For conversations, use `ConversationSender` and `SqliteConversationStore` the same way the CLI
does internally (see `SemaNami.Cli/Program.cs`).

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
