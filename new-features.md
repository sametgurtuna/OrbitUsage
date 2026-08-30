# Orbit - New Feature Proposals

This document lists proposed features for Orbit following Phase 1 (Claude integration complete), detailing how each should work and prioritized with 1-5 ratings.

**Scoring:** Impact and Effort from 1 (lowest) to 5 (highest). Priority = Impact - Effort + 3 (for reference; close to 5 = immediate priority, close to 1 = can be deferred).

| # | Feature | Impact | Effort | Priority | Category |
|---|---------|:---:|:---:|:---:|---|
| 1 | Threshold notifications (Windows toast) | 5 | 2 | **6** | Functional |
| 2 | Usage history / trend chart (Sparklines) | 4 | 3 | **4** | Functional |
| 3 | Drag-and-drop repositioning | 3 | 3 | **3** | UX |
| 4 | Gauge micro-interactions (rich tooltips, click-detail) | 4 | 2 | **5** | UX |
| 5 | Light theme support | 3 | 2 | **4** | Visual |
| 6 | Acrylic / blur background effect | 3 | 3 | **3** | Visual |
| 7 | Complete ChatGPT & Gemini providers | 5 | 4 | **4** | Functional |
| 8 | Multi-monitor support | 3 | 3 | **3** | Functional |
| 9 | Quick tray actions (Snooze, Pause) | 3 | 2 | **4** | UX |
| 10 | Automatic update mechanism | 4 | 5 | **2** | Infrastructure |
| 11 | Per-service custom refresh interval | 2 | 2 | **3** | Functional |
| 12 | Export / Import settings (backup) | 2 | 1 | **4** | Infrastructure |

---

## 1. Threshold notifications (Windows toast) - Priority 6 ⭐

**What:** When a service's usage percentage crosses a defined threshold (e.g. 80% warning, 95% critical, 100% exhausted), a toast notification is displayed via Windows Notification Center.

**How it works:**
- Settings provides checkboxes for each service: "Notify: [ ] at 80% [ ] at 95% [ ] at 100%" (added to `ServiceSettings`).
- When a new result arrives in `UsageScraperService.RefreshNowAsync`, it is compared with the previous threshold; if crossed upwards, a notification is sent using `System.Windows.Forms.NotifyIcon.ShowBalloonTip`.
- Clicking the notification expands the notch window (`MainWindow.Expand()`).
- Cooldown logic ensures alerts are not spammed repeatedly until usage drops and rises again.

**Why high priority:** Directly aligns with Orbit's core value proposition ("never miss quota limits"); low effort, high impact.

## 2. Usage history / trend chart (Sparklines) - Priority 4

**What:** Mini sparkline chart under each gauge in the expanded panel showing the last 24 hours / 7 days of quota consumption.

**How it works:**
- Persist a small ring-buffer of `(timestamp, serviceKey, percent)` in `%LOCALAPPDATA%\Orbit\history.json` (e.g. last 500 records).
- Add a lightweight `Sparkline` sub-control to `RadialGauge` (using WPF `Polyline`, zero external dependencies).
- Optional toggle in Settings to preserve compact layout.

## 3. Drag-and-drop repositioning - Priority 3

**What:** Direct on-screen dragging and snapping of the notch instead of numerical offset sliders alone.

**How it works:**
- "Reposition..." button in Settings enables drag mode.
- On mouse release, snaps to the nearest screen edge (top/right/left) and computes the offset.
- Writes to `NotchOffsetX` and `NotchOffsetY`.

## 4. Gauge micro-interactions - Priority 5

**What:** Modern rich glassmorphic tooltip on hover, click-to-open detail popup, and neon pulse glow animation upon refresh completion.

**How it works:**
- Modern dark `ToolTip` style with service accent dot, bold percentage, reset countdown, and last updated time.
- Click triggers a quick popup with shortcuts to log in or open usage in browser.
- 300ms flash glow animation on gauge update.

## 5. Light theme support - Priority 4

**What:** Support system theme sync or manual light/dark toggle.

**How it works:**
- Add `NotchTheme { System, Dark, Light }` enum to `AppSettings`.
- Move colors to `DynamicResource` brushes in `ResourceDictionary` files (Dark.xaml / Light.xaml).

## 6. Acrylic / blur background effect - Priority 3

**What:** Translucent desktop reflection blur using Windows 11 Mica / Acrylic DWM API.

**How it works:** Native interop call to `DwmSetWindowAttribute` in `NativeMethods.cs` with graceful fallback to solid dark brush on Windows 10.

## 7. Complete ChatGPT & Gemini providers - Priority 4

**What:** Fill in scraping selectors and provider implementations for ChatGPT and Gemini.

**How it works:** Implement `IUsageProvider` wrappers using `SelectorUsageScraper` and active selectors in `selectors.json`.

## 8. Multi-monitor support - Priority 3

**What:** Display notch on any active monitor selected from Settings.

**How it works:** `ScreenPositionHelper.Position` queries `Screen.AllScreens` and calculates working area coordinates for the chosen monitor device name.

## 9. Quick tray actions - Priority 4

**What:** Tray right-click options for "Snooze 1 hour" and "Pause auto-refresh".

**How it works:** Add context menu items in `TrayIconManager` controlling `UsageScraperService` timer state.

## 10. Automatic update mechanism - Priority 2

**What:** Automatic check for new GitHub Releases on launch with update notifications.

## 11. Per-service custom refresh interval - Priority 3

**What:** Separate polling intervals per service (e.g. Claude 15 min, ChatGPT 60 min).

## 12. Export / Import settings - Priority 4

**What:** Backup `settings.json` and `selectors.json` into a single `.orbitbackup.json` file for easy migration.
