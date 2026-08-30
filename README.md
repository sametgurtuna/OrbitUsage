# Orbit

Orbit is a lightweight Windows desktop utility that keeps your LLM subscription usage visible at
a glance. It lives at the edge of your screen as a small, macOS notch style bar. Hover over it to
reveal a set of animated radial gauges, one per service, each filling smoothly to show the
percentage of your quota used.

Phase 1 ships with a complete, working integration for **Claude**. **Antigravity** (Google's
desktop agentic IDE - a separate usage quota from the Gemini web app, not the same thing) now has
a real provider too, though its selector is unverified against a live session (see "Fixing a
broken selector" below) so it defaults to Manual mode until someone fills it in. ChatGPT still
appears in the UI as a clearly labeled "not implemented yet" placeholder. The provider architecture
(`IUsageProvider` plus `selectors.json`) is built so wiring up ChatGPT later is a contained
addition, not a rewrite.

## Why it exists

None of Claude, ChatGPT, or Antigravity currently publish an official API for a consumer account's
subscription usage. For Claude and (once implemented) ChatGPT, Orbit works around that by driving
a small, isolated, embedded browser session (WebView2) that loads each service's own usage page and
reads the percentage straight out of the page. Antigravity is a desktop app rather than a web page,
so it's read a different way - via the Chrome DevTools Protocol, see "Antigravity setup" below.

For Claude/ChatGPT, you log in once, in a normal browser window that Orbit opens for you, and Orbit remembers
that session for future background refreshes.

Because this depends on the structure of a web page that the provider can change at any time, the
selector used to find the usage number is kept in an external, editable file rather than compiled
into the app. If a service updates its page and Orbit stops finding a number, you can fix it
yourself in a couple of minutes with your browser's developer tools, no rebuild required. A manual
entry mode is also built in as a fallback for when scraping simply is not an option.

## What it looks like

Orbit has two layouts, switchable at any time from Settings without restarting the app.

* **Top center.** A horizontal pill docked at the top center of your primary monitor, in the
  spirit of a MacBook's notch. Expands downward into a row of gauges.
* **Right center.** A vertical dock hugging the right edge of the screen, vertically centered.
  Expands into a column of gauges. Useful when the top of your screen is already busy with other
  menu bar style utilities.

Both states, collapsed and expanded, are connected by a short, eased width and height animation.
Each gauge's ring fills with its own smooth animation whenever a new percentage comes in, rather
than snapping instantly, and its color shifts from a calm green through amber to red as usage
approaches the limit.

## Requirements

* Windows 10 or 11
* [.NET 8 SDK](https://dotnet.microsoft.com/) or newer to build from source
* **Microsoft Edge WebView2 Runtime.** This ships as a system component on most current Windows 11
  machines already. If it is missing, Orbit still starts, and scraping fails with a clear,
  in app message until you install it from
  https://developer.microsoft.com/microsoft-edge/webview2/

## Building and running from source

```powershell
cd usage
dotnet build Orbit.slnx -c Debug
dotnet run --project src\Orbit\Orbit.csproj
```

A small dark pill appears at the top center of your primary monitor. Hover over it to expand it;
it closes again as soon as your mouse leaves.

## First time login (Claude, ChatGPT)

Usage scraping needs an active session with the service you are checking, stored in a profile that
is completely separate from your everyday Edge or Chrome profile (under
`%LOCALAPPDATA%\Orbit\WebView2`). Orbit never touches your normal browser's cookies, history, or
saved logins.

1. Open the tray icon (bottom right of the taskbar, you may need to click the small arrow to reveal
   hidden icons) and choose **Settings...**
2. Click **Log into Claude...**. A normal, visible browser window opens.
3. Sign in as you normally would, including any two factor prompt, then close that window.
4. Back in Settings, your session is saved locally. The notch shows real data on its next refresh,
   or you can force one immediately with **Reload Now** from the tray menu.

## Antigravity setup

Antigravity doesn't go through a browser login - Orbit connects to it locally instead, via the
Chrome DevTools Protocol (Antigravity is an Electron/VS Code-fork app under the hood, so it exposes
this the same way any Chromium app does). This has been confirmed working (2026-08) against a live
session; the bundled `selectors.json` already ships with a working selector, so most people can
skip straight to step 1 and 4.

1. Launch Antigravity with the command-line flag `--remote-debugging-port=9222` (must match
   `remoteDebuggingPort` in the Antigravity entry of `selectors.json`, default `9222`). Fully quit
   Antigravity first if it's already running - the flag only takes effect on launch, e.g.:
   ```powershell
   & "$env:LOCALAPPDATA\Programs\Antigravity\Antigravity.exe" --remote-debugging-port=9222
   ```
2. **Open Settings > Models in Antigravity itself and leave that screen open.** The quota figure
   Orbit reads only exists in the DOM while that screen is showing - Orbit has no way to open it
   for you (no UI automation), so a refresh that happens while you're elsewhere in the app will
   just report "Data unavailable" and keep the last known value.
3. In Orbit's Settings, click **Detect Antigravity...** to confirm the port is reachable, turn off
   Antigravity's Manual mode, then **Reload Now** from the tray menu.

If a future Antigravity update changes its UI and the bundled selector stops working: click
**Detect Antigravity...** to confirm the port itself is still reachable, then re-derive the
selector by inspecting Settings > Models with DevTools the same way as any other broken selector
(see below) - the element you want is under "Model Quota", shows a percentage like "58%", and note
that it's **"% remaining", not "% used"** (`invertPercent: true` in its selectors.json entry flips
that back to the usual meaning - keep that set if you re-derive the selector).

## Fixing a broken selector (Claude, ChatGPT)

Each service's usage percentage is read from its page using a CSS selector defined in
`%LOCALAPPDATA%\Orbit\selectors.json`, seeded on first run from the copy bundled inside the app at
`Resources\selectors.json`. If a service changes its page layout and the number stops showing up:

1. Open the usage page in any signed in browser (currently
   `https://claude.ai/settings/usage`).
2. Open developer tools (F12), switch to the Elements panel, and find the element that shows the
   usage percentage. Claude's page currently exposes this as a `role="meter"` element with an
   `aria-valuenow` attribute, which is what Orbit reads by default because it is a clean number
   rather than free form text.
3. Right click the element, choose Copy, then Copy selector.
4. Edit `%LOCALAPPDATA%\Orbit\selectors.json` and paste that selector into the Claude entry's
   `usageTextSelector` (and `waitForSelector`). Adjust `percentRegex` if the visible text is not a
   plain number, for example `"62% used"` versus `"You have used 62%"`.
5. In Settings, click **Reload selectors.json**, then **Reload Now** from the tray menu.

If a plan simply does not expose a usage percentage anywhere on its pages, switch that service to
**Manual mode** in Settings and enter the value yourself. The rest of the app treats a manual value
exactly the same as a scraped one, including the color thresholds and the gauge animation.

## Settings

Opened from the tray icon, **Settings...**:

* Per service enable and manual mode toggle, with a manual percentage slider (Claude is fully
  functional; Antigravity works too once its selector is filled in, Manual mode by default; ChatGPT
  is shown disabled with a clear "not implemented" note)
* Refresh interval, from 5 to 120 minutes, default 20
* Layout: top center or right center, applied immediately on Save
* Start with Windows, which adds or removes a `HKCU\...\Run` registry entry and needs no elevated
  permissions
* Reload `selectors.json` without restarting the app

Settings are stored at `%LOCALAPPDATA%\Orbit\settings.json`.

## Packaging a setup.exe

The `installer\Orbit.iss` script in this repository builds a conventional Windows installer using
[Inno Setup](https://jrsoftware.org/isinfo.php), a free tool.

1. Publish a self contained, single file build:

   ```powershell
   dotnet publish src\Orbit\Orbit.csproj -c Release -r win-x64 --self-contained true `
     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64
   ```

2. Install Inno Setup if you do not already have it, then compile the script:

   ```powershell
   iscc installer\Orbit.iss
   ```

   Or simply open `installer\Orbit.iss` in the Inno Setup Compiler GUI and press Compile.

3. The finished installer is written to `installer\output\OrbitSetup.exe`. It installs per user by
   default (no administrator rights required), creates Start Menu shortcuts, and offers an optional
   "start with Windows" checkbox during setup.

Because the build is unsigned, Windows SmartScreen will likely show a warning the first time
someone runs the installer or the app. Users can continue past it with "More info" then "Run
anyway." Getting rid of that warning requires a paid code signing certificate, which is outside the
scope of this project.

## Known limitations

* Primary monitor only. Orbit does not yet let you choose a specific display in a multi monitor
  setup.
* ChatGPT only remains a stub (`ChatGptUsageProvider` in `src\Orbit\Services` returns a clearly
  labeled "not implemented" result). Wiring it up follows the same pattern as
  `ClaudeUsageProvider` (both share their WebView2 scraping logic via `SelectorUsageScraper`),
  plus filling in its entry in `selectors.json`.
* Antigravity's selector is a best-effort placeholder, not yet confirmed against a live session.
  Unlike Claude/ChatGPT it isn't a web page at all - it's read via the Chrome DevTools Protocol
  (`ChromeDevToolsUsageScraper`), which only works while Antigravity is running with
  `--remote-debugging-port` (see "Antigravity setup"). `targetUrlContains`/`usageTextSelector` are
  intentionally left blank in `selectors.json` until someone inspects a live session. Manual mode
  is Antigravity's default for exactly this reason.
* No code signing, so Windows SmartScreen and some antivirus software may flag a freshly built or
  installed copy, especially one added to the Windows startup list.
* Scraping depends on the structure of a page you do not control. Treat a stopped or incorrect
  percentage as a sign that a selector needs updating, not as a crash, and keep the refresh
  interval reasonable to avoid looking like automated traffic to the service you are checking.

## Uninstalling

If you used the installer, uninstall Orbit the normal way through "Apps and features." If you built
it yourself, delete the app folder. Either way, remove `%LOCALAPPDATA%\Orbit` afterward to clear
settings, the selector file, and the isolated WebView2 login session. If you enabled "Start with
Windows," disable that first in Settings, or delete the `Orbit` value under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` by hand.

## A note on terms of service

Orbit works by automating a browser session against a page meant for a human to read, not against
a published, supported API. Whether that is acceptable under the terms of service of the account
you connect is between you and that provider; review their terms before pointing Orbit at an
account you care about. Keeping the refresh interval conservative and relying on the manual mode
fallback when in doubt are both deliberate design choices meant to keep Orbit's footprint small.
