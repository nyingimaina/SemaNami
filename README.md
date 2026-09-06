# TelegramNotifier

A small, general-purpose way to send yourself a Telegram message from any tool or script —
a build finishing, a long job completing, a deploy landing, anything.

Two pieces:
- **`TelegramNotifier.Core`** — a class library any .NET app/tool can reference directly.
- **`TelegramNotifier.Cli`** — a console app (`notify`) any script (PowerShell, bash, a CI
  step, a non-.NET tool) can invoke as a plain process.

## One-time setup

### 1. Create a bot

1. Open Telegram and search for **`@BotFather`** (official, verified).
2. Send `/newbot`.
3. Give it a display name (anything) and a username ending in `bot`.
4. BotFather replies with a **token** — a string like `123456789:AAH...xyz`. Save it.

### 2. Let the bot know who you are

Telegram bots can't message you until you've messaged them first.

1. Search for your new bot's username in Telegram and open a chat with it.
2. Send it any message (e.g. "hi").

### 3. Find your chat id

From this folder:

```powershell
$env:TELEGRAM_BOT_TOKEN = "your-token-here"
dotnet run --project src/TelegramNotifier.Cli -- --get-chat-id
```

This prints the chat id(s) found from messages your bot has received. If you only ever
messaged it once yourself, there'll be exactly one number — that's your chat id.

### 4. Set both environment variables permanently

So every tool that shells out to `notify` (or references `TelegramNotifier.Core` directly)
can find them without you re-exporting them each session:

**Windows (PowerShell, persists across sessions):**
```powershell
[Environment]::SetEnvironmentVariable("TELEGRAM_BOT_TOKEN", "your-token-here", "User")
[Environment]::SetEnvironmentVariable("TELEGRAM_CHAT_ID", "your-chat-id-here", "User")
```
Restart your terminal after this for it to take effect.

**Linux/macOS (add to `~/.bashrc` / `~/.zshrc`):**
```bash
export TELEGRAM_BOT_TOKEN="your-token-here"
export TELEGRAM_CHAT_ID="your-chat-id-here"
```

## Usage

### As a standalone command any tool/script can shell out to

Build a self-contained executable once:

```powershell
dotnet publish src/TelegramNotifier.Cli -c Release -o publish
```

Then from anywhere:

```powershell
publish\TelegramNotifier.Cli.exe "Build finished successfully"
```

Or during development, without publishing first:

```powershell
dotnet run --project src/TelegramNotifier.Cli -- "Build finished successfully"
```

Exit code is `0` on success, `1` on failure (bad/missing config, blank message, send failure)
— safe to check from a script (`if ($LASTEXITCODE -ne 0) { ... }` / `if [ $? -ne 0 ]; then ...`).

### From another .NET project directly

Reference `TelegramNotifier.Core` and use `Notifier` — no process-spawn overhead:

```csharp
var sender = new TelegramBotMessageSender(botToken);
var notifier = new Notifier(sender, chatId);
await notifier.NotifyAsync("Deploy complete");
```

## Running the tests

```powershell
dotnet test
```
