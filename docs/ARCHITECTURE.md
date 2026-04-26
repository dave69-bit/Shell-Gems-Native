# Architecture

## Why Angular Can't Call DLLs Directly
Angular runs inside WebView2's browser context — a sandboxed Chromium browser context.
Browser sandboxes have no access to the OS, filesystem, or native libraries by design.
DLL calls require .NET 8.0 APIs (`require`, native modules) which are only available in the
**Main process**. The solution is IPC: Angular sends a message, Main calls the DLL, returns the result.

## System Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                        .NET/WebView2 APP (one process tree)               │
│                                                                      │
│  ┌─────────────────────────────┐      ┌───────────────────────────┐  │
│  │     RENDERER PROCESS        │      │       MAIN PROCESS        │  │
│  │     (Angular UI)            │      │       (.NET 8.0)           │  │
│  │                             │ IPC  │                           │  │
│  │  - Plugin grid              │◄────►│  - Scans plugins/dlls/    │  │
│  │  - Function selector        │      │  - Loads DLLs via Native C# Reflection │  │
│  │  - Side panel               │      │  - Handles IPC channels   │  │
│  │  - Dynamic form controls    │      │  - File system access     │  │
│  │  - JSON result viewer       │      │  - Reflection dispatch    │  │
│  │  - File browse + Base64     │      │                           │  │
│  └─────────────────────────────┘      └───────────────────────────┘  │
│                                                                      │
│                    plugins/                                          │
│                    ├── dlls/        ← .dll files live here           │
│                    └── icons/       ← plugin icon images             │
└──────────────────────────────────────────────────────────────────────┘
```

## Tech Stack

| Layer | Technology | Reason |
|-------|-----------|--------|
| UI Framework | Angular 18.2.9 | Component architecture, two-way binding for dynamic forms |
| Desktop Shell | .NET/WebView2 | Exposes .NET 8.0 APIs to a web UI |
| DLL Interop | `Native C# Reflection` | Loads and calls .NET DLLs from .NET 8.0 main process |
| IPC Bridge | .NET/WebView2 `WebView2 Interop` + `CoreWebView2` | Secure renderer↔main communication |
| Styling | SCSS + CSS Grid | Responsive layout, scoped styles per component |
| Bundler | Angular CLI + .NET 8.0 Publish | Single portable output folder |

## Plugin Model

Each plugin DLL is a self-contained unit that exposes **multiple named functions**.
The Shell never knows what a function does — it only knows its parameter schema and
how to display the JSON result it returns.

The DLL has two distinct layers:

**Public layer (Native C# Reflection boundary):** Three methods with the mandatory `Task<object> Method(dynamic input)` signature. Each is a thin wrapper that unpacks the `dynamic` payload and immediately delegates to the private layer. No logic lives here.

**Private layer (no dynamic):** Clean, typed methods with explicit signatures. All business logic, parameter casting, and reflection dispatch live here.

```
Plugin DLL — Public surface (Native C# Reflection boundary)
├── GetFunctions(dynamic)  →  delegates → GetFunctions()
├── GetParams(dynamic)     →  unpacks functionName → GetParams(string)
└── Execute(dynamic)       →  unpacks functionName + parameters
                                    → Execute(string, IDictionary<string, object>)

Plugin DLL — Private implementation (no dynamic)
├── GetFunctions()
├── GetParams(string functionName)
├── Execute(string functionName, IDictionary<string, object> parameters)  ← reflection dispatcher
│       calls private method by name via BindingFlags.NonPublic | Instance
│
├── Compress(IDictionary<string, object>)        → returns JSON-serializable object
├── ExtractMetadata(IDictionary<string, object>) → returns JSON-serializable object
└── ValidateSchema(IDictionary<string, object>)  → returns JSON-serializable object
```

`dynamic` never propagates past the public boundary. All type casting happens once, at the top of each public method, before delegation.

## IPC Communication Pattern

```
Angular Component
      │
      ▼
Angular Service (e.g. PluginService)
      │  calls window.chrome.webview.invoke('channel:name', payload)
      ▼
MainForm.cs  (WebView2 Interop exposes electronAPI)
      │
      ▼
main/ipc/handlers.ts  (CoreWebView2.WebMessageReceived)
      │
      ├── plugins:list       → pluginScanner.ts
      ├── plugins:functions  → pluginLoader.ts → DLL.GetFunctions()
      ├── plugins:params     → pluginLoader.ts → DLL.GetParams(functionName)
      └── plugins:execute    → pluginLoader.ts → DLL.Execute(functionName, params)
                                                      │
                                                      └── DLL uses reflection
                                                          to call internal method
```

**Rule:** Angular components never call `window.chrome.webview` directly.
They always go through an Angular service. This keeps components testable and decoupled.

## User Interaction Flow

```
1. App loads → plugins:list → plugin grid rendered
       │
       ▼
2. User clicks plugin → plugins:functions → function list rendered in side panel
       │
       ▼
3. User selects a function → plugins:params → dynamic form rendered for that function
       │
       ▼
4. User fills form → clicks Execute → plugins:execute → JSON result displayed in panel
       │
       ▼
5. User can select a different function (step 3) or close the panel
```

The side panel is divided into three regions:
- **Function selector** (top) — tab/list of available functions
- **Parameter form** (middle) — dynamic controls for the selected function
- **Result viewer** (bottom) — formatted JSON output, appears after Execute

## Folder Structure

```
shell-gems/
│
├── CLAUDE.md
├── docs/
│   ├── PRD.md
│   ├── ARCHITECTURE.md          ← This file
│   ├── API_CONTRACT.md
│   ├── UI.md
│   └── PROGRESS.md
│
├── host/                        ← .NET 8.0 WinForms Host
│   ├── Program.cs               ← App entry point
│   ├── MainForm.cs              ← WebView2 configuration + IPC handlers
│   ├── PluginManager.cs         ← Scans plugins/dlls and loads via Reflection
│   ├── ShellGems.Host.csproj
│   └── appsettings.json
│
├── renderer/                    ← Angular Frontend (v18.2.9)
│   ├── angular.json             ← Uses Webpack-based 'browser' builder
│   ├── package.json
│   ├── tsconfig.json
│   └── src/
│       ├── main.ts
│       └── app/
│           ├── app.component.ts
│           ├── services/
│           │   ├── plugin.service.ts    ← Wraps window.chrome.webview.postMessage
│           │   └── file.service.ts      ← File reading + Base64 encoding
│           └── components/
│               ├── plugin-grid/         ← Home screen icon grid
│               ├── side-panel/          ← Sliding drawer (hosts all 3 regions)
│               ├── dynamic-form/        ← Builds controls from param schema
│               └── result-viewer/       ← Formatted JSON output display
│
└── plugins/                     ← Plugin deployment area
    ├── dlls/
    └── icons/
```

## Portability Design

The app is built as a **self-contained portable package** using .NET 8.0 Publish:

- Output: a single folder (`dist/win-unpacked/`) that can be zipped and copied anywhere
- No installer required — just run `ShellPlugin.exe`
- All Node modules bundled — no `npm install` needed on target
- No .NET runtime needed — `Native C# Reflection` bundles the CLR host
- All paths resolved via `app.getAppPath()` — never hardcoded
- `plugins/` folder is copied into the dist output and resolved at runtime

### Portability Rules for Claude
- Never use `__dirname` alone — always combine with `app.getAppPath()`
- Never reference `node_modules` paths at runtime
- Never use `process.cwd()` for plugin resolution
- The plugins path is always: `path.join(app.getAppPath(), 'plugins')`

## Key Design Decisions

### No Separate Backend Process
All logic lives in the .NET WebView2 Host app.
This eliminates the need to manage a separate server process, port conflicts, or startup ordering.

### WebView2 Interop (not nodeIntegration)
`nodeIntegration: false` is enforced. Angular accesses .NET 6 only through the
whitelisted `WebView2 Interop` API defined in `MainForm.cs`.

### Native C# Reflection for DLL Interop
`Native C# Reflection` is the most reliable way to call .NET DLLs from .NET 6 without a separate process.
It hosts the CLR inside the .NET 6 process. Called only from the main process.

### Reflection-Based Dispatch Inside the DLL
The Shell does not need to know anything about what a function does.
It passes `{ functionName, params }` to `Execute()` and the DLL uses
`BindingFlags.NonPublic | BindingFlags.Instance` reflection to route to the right method.

The DLL uses a two-layer design to keep `dynamic` contained at the Native C# Reflection boundary:
- Public methods (`dynamic input`) unpack the payload and immediately delegate — no logic
- Private methods use typed signatures (`string`, `IDictionary<string, object>`) — all logic lives here

This means adding a new function to a plugin requires **zero Shell changes** —
only a new private method and an entry in `GetFunctions()`.

### Function Schema Ownership
The DLL owns the parameter schema for each of its functions.
The Shell never hardcodes parameter names, types, or validation rules.
All of this comes from `GetParams(functionName)` at runtime.

### Result as Opaque JSON
The Shell treats the `result` object from `Execute()` as an opaque JSON structure.
It renders it in a syntax-highlighted, collapsible JSON viewer — it does not interpret keys.
This means the DLL author has full freedom over what the result shape looks like.

### File as Base64
Files selected by the user are read in the renderer via the File API (no fs needed),
encoded to Base64, and passed to the DLL as a string. Decoding is the DLL's responsibility.

## Port Configuration
No port needed — IPC replaces HTTP. There is no localhost server.

