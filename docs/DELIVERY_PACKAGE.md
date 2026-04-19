# Delivery Package — Offline Installation Bundle

## Goal
Produce a single folder called `Shell-Gems-Delivery/` that a technician can copy to
any Windows 10/11 machine (with nothing pre-installed) and run the app with zero internet access.

---

## What Needs to Be Bundled

### 1. The App Itself
The .NET/WebView2 portable build output — a self-contained folder, no installer needed.
Built via: `npm run build:portable` (configured in `scripts/build.js` using `.NET/WebView2-packager`)
Output folder: `dist/win-unpacked/`
Rename to: `Shell-Gems/`

The .NET/WebView2 portable build already bundles:
- Chromium renderer (no Chrome needed on target)
- .NET 6 runtime (no .NET 6 needed on target)
- All npm dependencies (no npm install needed on target)
- Angular compiled output (no Angular CLI needed on target)

### 2. Visual C++ Redistributable (REQUIRED)
.NET/WebView2 and Native C# Reflection both depend on Visual C++ runtime DLLs.
On a completely clean Windows machine these are NOT present.

- File: `VC_redist.x64.exe`
- Version: **Visual C++ Redistributable for Visual Studio 2015–2022 (x64)**
- Download from (on dev machine):
  https://aka.ms/vs/17/release/vc_redist.x64.exe
- Size: ~25MB
- Must be run ONCE on target machine before launching Shell-Gems.exe
- Silent install flag: `/quiet /norestart`

### 3. .NET Runtime (REQUIRED for Native C# Reflection DLL calls)
Native C# Reflection hosts the CLR to call .NET DLLs. The target machine needs .NET installed.

- File: `dotnet-runtime-installer.exe`
- Version: **.NET 6.0 Runtime (Windows x64)** — minimum for Native C# Reflection compatibility
- Download from (on dev machine):
  https://dotnet.microsoft.com/en-us/download/dotnet/6.0
  → choose "Run desktop apps" → Windows x64 → .exe installer
- Size: ~55MB
- Silent install flag: `/quiet /norestart`

> Note: If your DLL plugins target a specific .NET version (e.g. .NET Framework 4.8
> instead of .NET 6), replace the above with that version's runtime installer.
> Confirm with whoever built the DLL plugins.

### 4. Plugin DLL Files
The actual plugin DLLs that the app will load.
Place them in: `Shell-Gems/plugins/dlls/`
Place their icons in: `Shell-Gems/plugins/icons/` (filename must match plugin ID)

---

## Final Delivery Folder Structure

```
Shell-Gems-Delivery/
│
├── Shell-Gems/                        ← The app (copy entire folder to target)
│   ├── Shell-Gems.exe                 ← Launch this to run the app
│   ├── plugins/
│   │   ├── dlls/                      ← Your .dll plugin files go here
│   │   └── icons/                     ← Plugin icon images
│   └── (all other .NET/WebView2 files)
│
├── prerequisites/                     ← Run these ONCE before first launch
│   ├── VC_redist.x64.exe              ← Visual C++ Redistributable 2015-2022
│   └── dotnet-runtime-installer.exe   ← .NET 6.0 Runtime (or required version)
│
└── INSTALL.md                         ← Technician instruction file (plain text)
```

---

## How to Build This Package (on Developer Machine)

Run these steps on your development machine before handing off:

```bash
# Step 1 — Install dependencies
npm install

# Step 2 — Build Angular renderer
cd renderer
ng build --configuration production
cd ..

# Step 3 — Build .NET/WebView2 portable package
npm run build:portable
# Output: dist/win-unpacked/

# Step 4 — Assemble delivery folder
mkdir Shell-Gems-Delivery
mkdir Shell-Gems-Delivery\prerequisites

# Copy app
xcopy dist\win-unpacked Shell-Gems-Delivery\Shell-Gems /E /I

# Copy prerequisites (download these manually first — see above)
copy VC_redist.x64.exe Shell-Gems-Delivery\prerequisites\
copy dotnet-runtime-installer.exe Shell-Gems-Delivery\prerequisites\

# Copy instruction file
copy INSTALL.md Shell-Gems-Delivery\

# Step 5 — Zip it
# Right-click Shell-Gems-Delivery → Send to → Compressed (zipped) folder
# Or use 7-Zip: 7z a Shell-Gems-Delivery.zip Shell-Gems-Delivery\
```

---

## Checklist Before Handing Off

- [ ] `Shell-Gems.exe` launches correctly on your own machine from the delivery folder
- [ ] `plugins/dlls/` contains all required .dll files
- [ ] `plugins/icons/` contains all icon images (matching plugin IDs)
- [ ] `prerequisites/VC_redist.x64.exe` is present
- [ ] `prerequisites/dotnet-runtime-installer.exe` is present and correct version
- [ ] `INSTALL.md` is in the root of the delivery folder
- [ ] Tested on at least one clean Windows 10 or 11 VM before delivery

