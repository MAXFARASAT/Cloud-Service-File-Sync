# Submission Checklist (Requirement Mapping)

## Functional Requirements

- **CFAPI implementation in native C++ (mandatory)**  
  - `src/FileSync.Interop.Native/FileSyncInteropNative.cpp`
- **Interop C++ <-> C# via P/Invoke**  
  - `src/FileSync.Core/Interop.cs`
- **Expose operations to C#**  
  - Register sync root: `RegisterSyncRoot` in `src/FileSync.Interop.Native/FileSyncInteropNative.cpp` and `RegisterRootOrThrow` in `src/FileSync.Core/Interop.cs`  
  - Create placeholder files: `CreatePlaceholderFile` and `CreatePlaceholderOrThrow`  
  - Trigger hydration: `TriggerHydration` and `TriggerHydrationOrThrow`  
  - Notify file state changes: `NotifyFileStateChange` and `NotifyStateOrThrow`
- **Drag-and-drop upload**  
  - `src/FileSync.App/MainWindow.xaml`  
  - `src/FileSync.App/MainWindow.xaml.cs`  
  - `src/FileSync.App/MainViewModel.cs` (`HandleDropAsync`)
- **Cloud sync using CFAPI placeholders**  
  - `src/FileSync.Core/SyncCoordinator.cs` (`UploadAndCreatePlaceholderAsync`)
- **File listing UI (name, size, sync status, auto-refresh)**  
  - `src/FileSync.App/MainWindow.xaml`  
  - `src/FileSync.App/MainViewModel.cs`
- **Context menu integration for Explorer upload**  
  - `scripts/register-context-menu.ps1`  
  - `src/tools/FileSyncUploader/Program.cs`
- **On-demand download / hydration**  
  - `src/FileSync.App/MainWindow.xaml` (`Hydrate Selected` button)  
  - `src/FileSync.App/MainViewModel.cs` (`HydrateSelected`)  
  - `src/FileSync.Core/SyncCoordinator.cs` (`Hydrate`)  
  - `src/FileSync.Interop.Native/FileSyncInteropNative.cpp` (`TriggerHydration`)
- **Status indicators: Synced/Pending/Downloaded**  
  - `src/FileSync.Core/Models.cs` (`SyncStatus`)  
  - bound UI in `src/FileSync.App/MainWindow.xaml`

## Technical Requirements

- **Frontend WPF (.NET 6+)**  
  - `src/FileSync.App/FileSync.App.csproj`
- **MVVM preferred**  
  - `src/FileSync.App/MainViewModel.cs`
- **LocalStack backend (S3 simulation)**  
  - `src/FileSync.Core/S3SyncService.cs`  
  - `scripts/setup-localstack.ps1`
- **Storage (JSON/SQLite)**  
  - JSON storage in `src/FileSync.Core/MetadataStore.cs`

## Exception Handling

- **Logging + safe failure handling + user messages**  
  - `src/FileSync.Core/Logging.cs`  
  - `src/FileSync.Core/SyncCoordinator.cs`  
  - `src/FileSync.App/MainViewModel.cs`
- **C++ errors propagated to C#**  
  - HRESULT returns in `src/FileSync.Interop.Native/FileSyncInteropNative.cpp`  
  - exception translation in `src/FileSync.Core/Interop.cs`

## Build/Run Packaging

- **Solution file**  
  - `FileSyncPrototype.sln`
- **Architecture document**  
  - `ARCHITECTURE.md`
- **Setup and run guide**  
  - `README.md`
