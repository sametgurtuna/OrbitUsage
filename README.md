# Orbit

Orbit is a lightweight Windows desktop utility that keeps your LLM subscription usage visible at
a glance. It lives at the edge of your screen as a small, macOS notch style bar. Hover over it to
reveal a set of animated radial gauges, one per service, each filling smoothly to show the
percentage of your quota used, reset countdown timer, and status.

Orbit also features a built-in **Local REST API server** (`http://127.0.0.1:18923`) and **CLI mode** (`orbit status`), allowing you to integrate live quotas into **Elgato Stream Deck**, **Rainmeter**, or terminal scripts!

---

## Features

- **Dynamic Notch & Radial Gauges:** Smooth, floating notch with 1.1s sweep fill animation and custom brand color accents (Claude terracotta `#D97757`, ChatGPT off-white `#D8D8E0`, Antigravity blue `#3B82F6`).
- **Reset Countdown Timers:** Automatically extracts reset times (e.g. `in 2h 15m`) and displays countdown badges and rich tooltips.
- **Threshold Alerts (Windows Toast / Balloon Notifications):** Receive proactive alerts when quotas cross 80%, 95%, or 100% thresholds. Clicking the notification expands the notch.
- **Multi-Monitor Support:** Choose any connected monitor from Settings with live positioning preview.
- **Local REST API & Web Dashboard:** Built-in HTTP server on `http://127.0.0.1:18923/api/usage` for Stream Deck, Rainmeter skins, and custom developer tools.
- **Terminal CLI Mode:** Run `orbit status` or `orbit refresh` from PowerShell / Command Prompt to inspect live quotas via terminal ASCII banner or JSON.
- **Robust Scrapers:** Dual scraper engine supporting isolated WebView2 sessions (Claude, web portals) and Chrome DevTools Protocol / CDP WebSocket (Google Antigravity IDE on port 9222).

---

## CLI & Local REST API Integration

### Terminal CLI Commands

Orbit doubles as a fast command-line utility when invoked with arguments:

```powershell
# Print colorful ASCII quota status banner in terminal
orbit status

# Output raw JSON for scripting
orbit status --json

# Trigger an immediate background refresh across all services
orbit refresh

# Print CLI help
orbit --help
```

### Local REST API Endpoints

When Orbit is running, it hosts a lightweight REST API on `http://127.0.0.1:18923/` (CORS enabled):

| Endpoint | Method | Description |
|---|:---:|---|
| `/` | `GET` | Interactive HTML Web Dashboard & live status cards |
| `/api/usage` | `GET` | Full JSON state of all services with percentages, reset times, and colors |
| `/api/usage/{service}` | `GET` | Single service JSON (e.g. `/api/usage/claude`, `/api/usage/antigravity`) |
| `/api/ascii` | `GET` | Plaintext ANSI progress bar report for `curl` |
| `/api/status` | `GET` | Overall aggregate health status and service count |
| `/api/refresh` | `POST` | Trigger an immediate scraper refresh cycle |

#### Example: Stream Deck Setup
Using the standard **Stream Deck HTTP Request** or **Generic Web Action** plugin:
- Set URL to: `http://127.0.0.1:18923/api/usage/claude`
- Value Path: `$.displayText` (shows `42%` or `⏳ in 2h 15m`)
- Tap Action: `POST http://127.0.0.1:18923/api/refresh`

---

## Requirements

* Windows 10 or 11
* [.NET 8 SDK](https://dotnet.microsoft.com/) or newer to build from source
* **Microsoft Edge WebView2 Runtime.** (Ships with Windows 11 by default)

---

## Building and Running from Source

```powershell
dotnet build Orbit.slnx -c Debug
dotnet run --project src\Orbit\Orbit.csproj
```

A small dark pill appears at the edge of your screen. Hover over it to expand it; it closes smoothly as soon as your mouse leaves.

---

## First Time Login (Claude, ChatGPT)

Usage scraping runs inside an isolated browser profile (`%LOCALAPPDATA%\Orbit\WebView2`). Orbit never touches your personal browser's cookies or history.

1. Open the tray icon in the taskbar and choose **Settings...**
2. Click **Log into Claude...**. A browser window opens.
3. Sign in to your account, then close the window.
4. Orbit saves the session locally and automatically polls for updates in the background.

---

## Antigravity Setup (Chrome DevTools Protocol)

Orbit connects to Antigravity via the Chrome DevTools Protocol over WebSocket on port 9222.

1. Launch Antigravity with `--remote-debugging-port=9222`:
   ```powershell
   & "$env:LOCALAPPDATA\Programs\Antigravity\Antigravity.exe" --remote-debugging-port=9222
   ```
2. **Open Settings > Models in Antigravity** to keep the quota counters active in the DOM.
3. In Orbit's Settings, click **Detect Antigravity...** to verify connection, uncheck Manual Mode, and enjoy live quota syncing!

---

## Packaging a setup.exe

1. Publish a self-contained, single-file Release binary:
   ```powershell
   dotnet publish src\Orbit\Orbit.csproj -c Release -r win-x64 --self-contained true `
     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64
   ```

2. Compile the Inno Setup installer script:
   ```powershell
   iscc installer\Orbit.iss
   ```
   The installer will be generated at `installer\output\OrbitSetup.exe`.
