# SemaNami, explained simply

("Sema Nami" is Swahili-ish for "talk to me" — that's the whole idea.)

## What problem does this solve?

Imagine you kick off something that takes a while — a build, a deploy, a script
copying a million files. You don't want to sit there watching a terminal. You want
your computer to **text you** when it's done, the same way a friend would text you.

And sometimes it's not even one message — you want to have an actual back-and-forth.
Like:

> **Script:** "Deploy finished. Want me to restart the server? (yes/no)"
> **You (from your phone, at the gym):** "yes"
> **Script (checks back later, sees your reply, restarts the server)**

SemaNami is a tiny tool that lets any script or program on your computer send you
Telegram messages, and — the more interesting part — **read your replies back**,
even if you reply five minutes or five days later.

## The mental model

Think of SemaNami as giving every script on your machine a phone with your number
saved in it. The script can:

1. **Text you** and not wait for a reply (a notification).
2. **Text you and start a conversation**, walk away, and check back later for your reply.
3. **Text you and literally wait on the line** until you reply (or give up after a timeout).

That's really the whole tool. Everything else in it (Telegram bots, background
services, SQLite storage) is just the plumbing that makes those three things work
reliably, even across reboots.

## Example 1: "Just tell me when you're done" (fire and forget)

Say you have a PowerShell build script. Add one line at the end:

```powershell
SemaNami -sender "Build Script" -message "Build finished successfully"
```

That's it. It sends the Telegram message and exits immediately. It doesn't care if
you reply or not — like a notification ping.

## Example 2: "Ask me something, I'll answer when I get to it"

Now say your deploy script wants permission before doing something risky, but it's
fine to wait:

```powershell
SemaNami -sender "Deploy" -message "Ship it?" -conversation deploy-42
```

This starts a conversation named `deploy-42` and, just like example 1, returns
right away — it does **not** sit there waiting. Later — could be a different script,
a different day — something checks whether you've replied:

```powershell
SemaNami --conversation-history deploy-42 -sender "Deploy"
```

This prints out the messages in that conversation as JSON. If you've replied "yes"
on your phone, it'll be right there. You can call this over and over (e.g. every
hour from a scheduled task) — it's just reading, it never blocks or hangs.

## Example 3: "Ask me and actually wait for the answer"

Sometimes the script really can't move on without you. For that:

```powershell
SemaNami --wait-for-reply -sender "Deploy" -conversation deploy-42 --timeout 300
```

This blocks — the script pauses right here — until you reply on Telegram, or until
300 seconds pass with no reply (in which case it gives up and exits with a special
error code so your script knows to handle that case).

## How does it know it's *you* replying, and not spam?

When you first set it up, you:

1. Create a Telegram bot (Telegram makes you talk to a bot called `@BotFather` and
   type `/newbot` — this is the one manual step, nobody can automate it for you).
2. Give SemaNami the token BotFather gives you.
3. Message your new bot once, so Telegram knows it's allowed to message you back.

After that, `SemaNami --setup` figures out the rest (your chat ID, saving the
config) automatically. You only do this once per computer.

```powershell
SemaNami --setup
```

## Why is there a "background listener"?

If a script asks a question and then the whole computer goes to sleep, or the
script's process ends, *something* still needs to be watching Telegram so your
reply doesn't get lost. That's the background listener — a small process that
starts automatically at login (via Task Scheduler on Windows, `launchd` on macOS,
`systemd --user` on Linux) and just sits there quietly catching replies and saving
them, so `--conversation-history` and `--wait-for-reply` always have something to
find. You never run it by hand — the installer sets it up for you.

## Using it straight from C# instead of the command line

If your tool is already a .NET program, you can skip spawning a separate process
and just call the library directly:

```csharp
var sender = new TelegramBotMessageSender(botToken);
var notifier = new Notifier(sender, chatId);
await notifier.NotifyAsync("Deploy complete");
```

Same idea as Example 1, just in-process.

## Cheat sheet

| I want to...                                   | Command |
|-------------------------------------------------|---------|
| Send a message, don't care about a reply         | `SemaNami -sender "X" -message "..."` |
| Start a conversation, check back later           | `SemaNami -sender "X" -message "..." -conversation my-id` |
| Read what's happened in a conversation so far    | `SemaNami --conversation-history my-id -sender "X"` |
| Wait right now until I reply (or time out)       | `SemaNami --wait-for-reply -sender "X" -conversation my-id` |
| Mark a conversation as done                      | `SemaNami --close-conversation my-id -sender "X"` |
| First-time setup                                 | `SemaNami --setup` |
| See all this again                               | `SemaNami --help` |

For the full details (exit codes, installer internals, building from source), see
the main [README.md](README.md).
