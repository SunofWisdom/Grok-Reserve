# Grok Reserve

A lean, self-contained Windows tray app that shows how much Grok subscription
capacity you have left.

The tray icon is the Grok mark with a ring around it. Green means reserve or on
pace; orange means deficit or exhausted — the same colours as the dashboard
percent. Hover for a three-line tooltip. Click for the dashboard.

## You need this first

Grok Build must be installed, and you must already be signed in:

```powershell
grok login
```

Grok Reserve reads `%USERPROFILE%\.grok\auth.json`. It does not have its own
account.

## Build a single self-contained exe

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

The exe lands in `bin\Release\net8.0-windows\win-x64\publish\GrokReserve.exe`.

## Run from source

```powershell
dotnet run
dotnet run -- --demo
```

`--demo` shows sample data without talking to Grok.

## Start with Windows

Settings → **Start with Windows**. That writes a per-user Run key to the current
exe path. If you move the exe, toggle the option off and on again.

## Independence

Independent project, MIT licensed. Not affiliated with xAI.

Windows port of the idea behind [Reserve](https://github.com/pocarles/reserve) by Pierre-Olivier Carles.
