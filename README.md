# Insominia

> ### Give your laptop Insominia and get your freedom back.

**Closing your laptop lid should turn off the screen, not kill your work.**

Insominia is a small Windows tray app. Flip one switch and you can shut the lid,
walk to your train, and whatever was running — an AI coding agent, a build, a
long export — keeps running to the end.

---

## The problem it solves

AI agent tasks are long. A single prompt can run for an hour. But people are
mobile and interruptible.

You are at a station. Your agent has 25 minutes of work left. Your train
arrives. You cannot stand there holding an open laptop — so you close it.

The moment the lid shuts, Windows sleeps, the process freezes, the network
drops, and 35 minutes of progress is stranded mid-task. Nothing was wrong with
the task. The only thing that happened is that **you physically moved.**

Insominia removes that failure.

---

## What v1 does

| Feature | Status |
|---|---|
| Block sleep while a task runs | Done — no admin needed |
| Make lid-close do nothing | Done — needs one-time admin setup |
| Auto-stop after a chosen time | Done (never / 30 min / 1 h / 2 h / 4 h) |
| Stop automatically on low battery | Done (default: below 15%) |
| Stop automatically when too hot | Done (default: above 90 °C, where sensors allow) |
| Battery saver — slow down apps you did not pick | Done |
| Pick which apps keep running | Done |
| Always restore your original lid setting | Done |
| Start with Windows | Optional |
| Diagnostics panel | Done |

v1 is a **manual switch**. You turn it on before you walk away. Automatic
detection of "an agent is running" is planned for v2.

---

## Install and run

No runtime download. No installer. Nothing to configure.

```powershell
.\build.ps1 -Run
```

The app appears as a small circle icon near the clock:

- **grey ring** — off, laptop behaves normally
- **green dot** — on, safe to close the lid
- **orange dot** — on, but something needs attention (low battery / high temp)

Right-click the icon for the menu. Double-click to toggle on and off.

### The one-time setup

Windows only lets an administrator change what the lid-close button does, so the
first time you need it, click **One-time setup** in the menu and approve the
Windows prompt.

That registers a tiny scheduled task which Insominia can trigger silently
afterwards. **You will never see a UAC prompt again** — the switch just works
from then on.

Without the setup, Insominia still blocks *idle* sleep, but closing the lid will
still put the laptop to sleep.

---

## Battery saver

Your agent needs one app running. Chrome, Slack and Teams do not need to keep
rendering and polling inside a closed laptop — that is pure wasted battery.

**Choose apps to keep running…** lets you tick the apps that must stay at full
speed. Everything else gets slowed right down while the lid is shut, and speeds
straight back up when you switch Insominia off.

### How it works, and why it is safe

Windows cannot sleep some apps and keep others awake — the CPU is either running
or it is not. So Insominia does the opposite: it keeps the system awake and
*holds back* the apps you did not pick.

It uses **EcoQoS**, the same mechanism Windows applies to background browser
tabs. The process moves to efficiency cores and clocks down. It stays alive and
stays connected — it is just slow and cheap.

Nothing is closed, nothing is frozen, no unsaved work is at risk.

Measured on a real laptop, with a process doing continuous arithmetic:

```
full speed      ~15,500k iterations/sec
throttled        ~3,300k iterations/sec    ← 79% less work, so far less power
restored        ~15,600k iterations/sec    ← straight back to normal
```

### The process-tree rule

An AI agent is never one process. If you protect your terminal, the `node`,
`python` and `git` processes it spawns must be protected too — otherwise
Insominia would throttle the very task it was asked to keep alive.

So ticking an app protects **its whole process tree**, including children
started after you switched on.

### What it will never touch

The shell, core OS processes, audio, and security software are permanently off
limits, along with anything not running as you. Throttling antivirus or
`explorer.exe` would make the machine feel broken.

### If it crashes

Throttled process IDs are written to disk. If Insominia is force-killed, the
next launch finds them and speeds them back up. Your apps can never be left
stuck in slow mode.

> One implementation note worth knowing, because it is a trap: switching EcoQoS
> off with `ControlMask = 0` means *"let Windows decide"*, and Windows will
> cheerfully keep a background process throttled forever. Insominia uses the
> explicit `ControlMask = EXECUTION_SPEED, StateMask = 0` instead — a hard
> "do not throttle this". This was caught by measurement, not by reading docs.

---

## Safety

A closed laptop running at full load cannot breathe, and on battery it drains
fast. Insominia takes that seriously:

- **Battery guard** — if you are unplugged and drop below 15%, it switches
  itself off and tells you, rather than letting the laptop die mid-task.
- **Heat guard** — above 90 °C it switches itself off.
- **Honest about sensors** — many laptops do not expose a temperature sensor to
  Windows. When that happens the app says *"sensor not available"* instead of
  pretending everything is fine. On those machines, **prefer running plugged in**
  and set an auto-stop limit.
- **Always restores your settings** — your original lid behaviour is saved
  before anything is changed, and put back when you switch off, exit, log off,
  or if the app crashes.

Insominia never leaves your laptop unable to sleep. That is the one bug it must
not have.

---

## Project layout

```
src/
  Program.cs          entry point, single instance, elevated-helper mode
  TrayApp.cs          the tray UI and all the logic
  TrayIcons.cs        icons drawn at runtime (no binary assets)
  KeepAwake.cs        SetThreadExecutionState - blocks sleep, no admin
  LidPolicy.cs        reads/writes the lid-close power setting
  ElevatedTask.cs     the one-time scheduled task, so UAC appears only once
  SafetyMonitor.cs    battery and temperature guards
  AppSettings.cs      settings file + logging
  StartupManager.cs   optional "start with Windows"
  app.manifest        asInvoker (the tray app is NOT elevated) + DPI aware

tools/
  diag/Diag.cs        console diagnostics - prints everything the app knows
  probe/Probe.cs      the original proof-of-concept for the sleep block

build.ps1             build script (uses the compiler built into Windows)
```

Settings and the log live in `%APPDATA%\Insominia\`.

### Build commands

```powershell
.\build.ps1              # build the app
.\build.ps1 -Diag        # also build the console diagnostics tool
.\build.ps1 -Run         # build and launch
.\build.ps1 -Clean       # delete build output
```

---

## Technical notes

- **Built with the C# compiler that ships with Windows** (.NET Framework 4.x),
  so there is nothing to install for you or your users, and the output is a
  single ~37 KB `.exe`.
- **Blocking sleep** uses `SetThreadExecutionState` with `ES_SYSTEM_REQUIRED`
  and `ES_AWAYMODE_REQUIRED`, deliberately **without** `ES_DISPLAY_REQUIRED` —
  the screen *should* go off; only the system must stay up.
- **Lid behaviour** is the power setting `5ca83367-…` in subgroup
  `4f971e89-…`, set to `0` (Do nothing). It is hidden by default on Modern
  Standby machines, so Insominia unhides it as part of setup.
- **Modern Standby (S0) laptops** often report *"Network Connected"* standby,
  which is good news for this use case — check yours with `powercfg /a`.

---

## Roadmap

**v2 — stop flipping the switch by hand**

- Detect a running agent task and turn on automatically, off when it finishes
- A small local endpoint so an editor or agent can say "I am working"
- Keep the network alive reliably across standby transitions
- Progress notification on your phone when the task completes

**v3**

- Checkpoint and resume, so even a forced sleep does not lose work
- Per-task history and timing
