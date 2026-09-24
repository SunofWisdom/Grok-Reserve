# Changelog

## 1.0.3

- Hover tip is our own card, not the Windows tooltip. Text is centered, in the same paper or charcoal as the dashboard, with a hairline edge.
- The large number is how much of the SuperGrok week is left, in the pace green or orange. Under it: pace, reset, and Bot only when that pool is signed in.
- The tip stays above the tray icon. It no longer follows the pointer or redraws on every mouse move.

## 1.0.2

Matches the two weekly meters on grok.com → Settings → Usage.

- **SuperGrok week** stays the tray ring and the big number. Reserve shows how much is **left** (so 9% used on the web is **91% left** here).
- Product rows follow the web breakdown: Chat, Imagine, and Automations (`GrokTasks`). Products the API does not report are no longer filled in as Ready.
- **Grok Bot** is its own weekly pool, with its own reset time. It is not part of the SuperGrok bar. Reserve reads it from the Grok Bot desktop app already signed in on this PC. If Bot is not installed or not signed in, that block stays hidden.
- The tray tooltip adds a `Bot …% left` line when that pool is available.
- On launch, a short notice points at the notification area, because Windows often hides a new tray icon.

## 1.0.1

- Keep the headline (`100% left`, `Not connected`) on a single line at high DPI.
- Always list Build, Imagine, Chat, and Voice. Unused products show as Ready.
- Stop treating Grok Bot as a slice of the shared SuperGrok weekly pool. Bot is a separate app with its own allowance; the Grok CLI billing feed does not report it, so Reserve no longer draws a fake Bot row.
- When the CLI JSON omits `creditUsagePercent`, fall back to grok.com `GetGrokCreditsConfig`.
- Screenshot on the README so the dashboard and Settings are visible on GitHub.

## 1.0.0

- First public Windows build: tray gauge, dashboard, Settings, Start with Windows.
