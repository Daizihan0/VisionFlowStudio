# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VisionFlow Studio is a Windows visual automation tool for desktop process automation. It uses screenshot-based visual positioning, pixel detection, and simulated input (mouse/keyboard) to automate workflows through a node-based flow designer.

Target: Windows 10/11 x64 desktop automation for office applications (not games/anti-cheat environments).

## Build Commands

```bash
# Build solution
dotnet build VisionFlowStudio.sln

# Build Release
dotnet build VisionFlowStudio.sln -c Release

# Run the application
dotnet run --project VisionFlowStudio.App/VisionFlowStudio.App.csproj
```

## Publish Commands

```powershell
# Portable build (recommended) - outputs to artifacts/publish/portable/
powershell -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1 -Mode portable

# Single file build - outputs to artifacts/publish/singlefile/
powershell -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1 -Mode singlefile
```

## Architecture

Three-project solution:

```
VisionFlowStudio.sln
├── VisionFlowStudio.Core/           # Core models and interfaces (no UI)
│   ├── Models/                      # FlowNode, FlowGraph, FlowConnection, RecordedInputEvent, etc.
│   └── Services/                    # IFlowExecutionEngine, IProjectStorageService, INodeTemplateLibraryService
├── VisionFlowStudio.Infrastructure/ # JSON storage, preview execution
│   └── Services/                    # JsonProjectStorageService, PreviewFlowExecutionEngine
└── VisionFlowStudio.App/            # WPF UI + real execution
    ├── ViewModels/                  # MainViewModel, FlowNodeViewModel
    ├── Services/                    # DesktopFlowExecutionEngine, InputSimulationService, VisionMatcherService, etc.
    └── MainWindow.xaml(.cs)         # Main UI
```

Dependencies: `.NET 8`, `WPF`, `OpenCvSharp4` (image matching), `System.Drawing.Common` (screen capture).

## Key Components

### Core Models (`VisionFlowStudio.Core/Models/`)

- **FlowNode**: Node model with Kind, Position, Settings (key=value pairs), AssetPayloadBase64 (template images)
- **FlowNodeKind**: Start, Action, Vision, Wait, Condition, SubFlow, End
- **FlowGraph**: Contains nodes and connections
- **FlowConnection**: Links nodes with source/target IDs and connector types
- **RecordedInputEvent**: Captured mouse/keyboard events for recording/playback

### Execution Engines

- **PreviewFlowExecutionEngine** (Infrastructure): Simulates flow without real input
- **DesktopFlowExecutionEngine** (App): Real execution using SendInput and OpenCV

### Key Services (`VisionFlowStudio.App/Services/`)

- **InputSimulationService**: Win32 SendInput for mouse/keyboard simulation
- **VisionMatcherService**: OpenCV template matching
- **ScreenCaptureService**: GDI screen capture
- **InputRecordingService**: Global hooks (WH_KEYBOARD_LL, WH_MOUSE_LL) for recording
- **SnippingOverlayWindow**: Full-screen overlay for screenshot region selection
- **CrosshairPickerWindow**: Full-screen overlay for manual pixel picking

## Node Types & Capabilities

| Kind | Purpose |
|------|---------|
| Start | Entry point |
| Action | Click, double-click, right-click, type text, hotkey, wheel |
| Vision | Image template matching to find screen position |
| Wait | Fixed delay, wait for pixel, wait for image appear/disappear |
| Condition | Branch based on image existence or pixel color |
| SubFlow | Replay recorded input events |
| End | Termination point |

## Global Hotkeys

- **F9**: Stop recording
- **F10**: Emergency stop (halts execution, stops recording if active)

## Critical Development Notes

### State Management
- **NEVER** modify FlowNode/FlowGraph during execution. Design-time data stays separate.
- Runtime state (anchor positions, match results) goes in separate RuntimeState, not node Settings.

### Image Assets
- Currently images are stored as Base64 in JSON. Future: externalize to `assets/images/` directory.
- Do NOT commit `bin/`, `obj/`, or `artifacts/` directories.

### Input Simulation
- Keep input operations **serial** - never parallel SendInput calls.
- Maintain delays between actions; don't remove them for speed.
- DPI-aware: Use `MOUSEEVENTF_VIRTUALDESK` and `MOUSEEVENTF_ABSOLUTE`.

### Recording/Playback
- Current implementation is raw event playback (fragile to window position changes).
- Future: event cleaning, anchor point substitution for robustness.

### Global Hooks
- Always ensure hooks are released on dispose/exit (idempotent cleanup).
- Handle exceptions in hook callbacks to prevent cascade failures.

### Branch Logic
- Condition nodes branch via `True`/`False` connectors.
- Other nodes use `Success`/`Failure`/`Next` connectors.

## Project File Structure

```
MyAutomationProject/
  project.json         # Project metadata
  flows/               # Flow definitions (future)
  assets/              # Template images, recordings (future)
  logs/                # Execution logs
```

Currently, all data is embedded in a single JSON file via `JsonProjectStorageService`.
