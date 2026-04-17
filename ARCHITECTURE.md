# Assignment Architecture Mapping

## Target Architecture

WPF App (C#) -> Interop (P/Invoke) -> Native Sync Engine (C++ CFAPI) -> LocalStack S3

## Architecture Diagram

```mermaid
flowchart LR
  subgraph UI["WPF App (C#)"]
    VM["MainViewModel (MVVM)"]
    Grid["DataGrid (file states)"]
  end

  subgraph NET["FileSync.Core (.NET)"]
    Coord["SyncCoordinator"]
    Interop["NativeSyncInterop (P/Invoke)"]
    S3["S3SyncService (LocalStack uploads)"]
    Meta["MetadataStore (JSON)"]
    Log["FileLogger"]
  end

  subgraph NATIVE["Native Sync Engine (C++ CFAPI)"]
    API["Exported CFAPI methods"]
  end

  subgraph CLOUD["LocalStack S3"]
    Bucket["S3 bucket (cfapi-files)"]
  end

  %% UI actions
  VM -->|"drag/drop upload"| S3
  VM -->|"Initialize Sync Root"| Coord
  VM -->|"Hydrate Selected"| Coord

  %% Orchestration to native engine
  Coord --> Interop --> API

  %% Native engine triggers hydration/download
  API -->|"hydration/download"| Bucket

  %% Cloud + local persistence
  S3 -->|"PUT objects"| Bucket
  Coord -->|"sync state updates"| Meta --> Grid

  %% Observability
  Coord --> Log
  API -->|"state change callbacks"| Coord
```

## Implemented Components

- `FileSync.App`:
  - WPF UI and MVVM-style `MainViewModel`
  - Drag-and-drop upload
  - Auto-updating DataGrid with file state
- `FileSync.Core`:
  - `NativeSyncInterop`: C# P/Invoke layer
  - `SyncCoordinator`: orchestration logic
  - `S3SyncService`: LocalStack upload service
  - `MetadataStore`: JSON persistence
  - `FileLogger`: app and exception logging
- `FileSync.Interop.Native`:
  - Native exported CFAPI methods:
    - `RegisterSyncRoot`
    - `CreatePlaceholderFile`
    - `TriggerHydration`
    - `NotifyFileStateChange`

## Error Handling Strategy

- Native functions return `HRESULT`.
- C# layer throws `NativeSyncException` for non-zero result.
- UI catches exceptions and surfaces user-friendly status messages.
- All failures are logged to file.
