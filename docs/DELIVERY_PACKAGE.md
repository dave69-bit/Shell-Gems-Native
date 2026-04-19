# Delivery Package — Offline Installation Bundle

## Goal
Produce a single portable folder called `Shell-Gems-Portable/` that a technician can copy to any Windows 10/11 machine (with nothing pre-installed) and run the app with zero internet access.

---

## What Needs to Be Bundled

### 1. The App Itself
The .NET Native WebView2 build output — a self-contained folder, no installer needed.
Built via standard `.NET 6 Publish` (`dotnet publish`).

Output folder: `Shell-Gems-Native/`

The portable build bundles:
- .NET Windows Forms Host executable
- Angular compiled UI output (placed in `renderer/dist/`)
- Custom external plugins (placed in `plugins/dlls/`)

### 2. WebView2 Runtime (REQUIRED)
The application relies on Microsoft Edge WebView2 to render the Angular frontend. Many modern Windows 10 and all Windows 11 machines have this pre-installed, but it must be included for clean offline environments.

- File: `MicrosoftEdgeWebview2Setup.exe`
- Version: **WebView2 Evergreen Standalone Installer (x64)**
- Download from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section
- Silent install flag: `/silent /install`

### 3. .NET Runtime (REQUIRED)
The host shell and plugins are written in native C#. The target machine needs .NET 6 installed.

- File: `dotnet-runtime-installer.exe` (technically `windowsdesktop-runtime-6.0.xx-win-x64.exe`)
- Version: **.NET 6.0 Desktop Runtime (Windows x64)** 
- Download from: https://dotnet.microsoft.com/en-us/download/dotnet/6.0
  (*Make sure to choose "Run desktop apps" -> Windows x64*)
- Silent install flag: `/quiet /norestart`


### 4. Plugin DLL Files
The actual plugin DLLs that the app will load.
Place compiled `.dll` files in: `Shell-Gems-Native/plugins/dlls/`
Place their associated icons in: `Shell-Gems-Native/plugins/icons/`

---

## Final Delivery Folder Structure

```
Shell-Gems-Portable/
│
├── Shell-Gems-Native/                 ← The app (copy entire folder to target)
│   ├── ShellGems.Host.exe             ← Launch this to run the app
│   ├── renderer/
│   │   └── dist/                      ← Angular UX web files
│   └── plugins/
│       ├── dlls/                      ← Your .dll plugin files go here
│       └── icons/                     ← Plugin icon images
│
├── prerequisites/                     ← Run these ONCE before first launch
│   ├── MicrosoftEdgeWebview2Setup.exe ← WebView2 Browser rendering engine
│   └── dotnet-runtime-installer.exe   ← .NET 6.0 Desktop Runtime
│
└── INSTALL.md                         ← Technician instruction file (plain text)
```

---

## How to Build This Package (on Developer Machine)

Run these steps on your development machine before handing off:

```powershell
$target = "Shell-Gems-Portable\Shell-Gems-Native"

# Step 1 — Build the Host Shell
dotnet publish host\ShellGems.Host.csproj -c Release -o "$target"

# Step 2 — Build Angular renderer
Push-Location renderer
npm install
npm run build
Pop-Location
Copy-Item -Path "renderer\dist" -Destination "$target\renderer\dist" -Recurse -Force

# Step 3 — Compile Plugins
dotnet publish MockPlugin\MockPlugin.csproj -c Release
Copy-Item "MockPlugin\bin\Release\net6.0\publish\mock-plugin.dll" "$target\plugins\dlls\" -Force

dotnet publish ChargingMockPlugin\ChargingMockPlugin.csproj -c Release
Copy-Item "ChargingMockPlugin\bin\Release\net6.0\publish\charging-plugin.dll" "$target\plugins\dlls\" -Force
```

---

## Checklist Before Handing Off

- [ ] `ShellGems.Host.exe` launches correctly on your own machine
- [ ] `plugins/dlls/` contains all required .dll files
- [ ] `plugins/icons/` contains all icon images (matching plugin IDs)
- [ ] `prerequisites/MicrosoftEdgeWebview2Setup.exe` is present
- [ ] `prerequisites/dotnet-runtime-installer.exe` is present and correct version
- [ ] `INSTALL.md` is in the root of the delivery folder
- [ ] Tested on at least one clean Windows VM before delivery
