# Changelog

## 1.0.1

- Keep the headline (`100% left`, `Not connected`) on a single line at high DPI.
- Always list Build, Imagine, Chat, and Voice. Unused products show as Ready.
- Stop treating Grok Bot as a slice of the shared SuperGrok weekly pool. Bot is a separate app with its own allowance; the Grok CLI billing feed does not report it, so Reserve no longer draws a fake Bot row.
- When the CLI JSON omits `creditUsagePercent`, fall back to grok.com `GetGrokCreditsConfig`.
- Screenshot on the README so the dashboard and Settings are visible on GitHub.

## 1.0.0

- First public Windows build: tray gauge, dashboard, Settings, Start with Windows.
