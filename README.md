# File Sync Prototype (CFAPI + WPF + LocalStack)

This project is a prototype implementation of a cloud file-sync app based on:

- WPF (.NET 6+, MVVM style)
- Native C++ CFAPI engine
- P/Invoke interop layer
- LocalStack S3 as cloud backend
- JSON metadata storage

## Project Structure

- `FileSyncPrototype.sln`: Visual Studio solution entry point
- `src/FileSync.App`: WPF desktop app (drag-and-drop upload, file listing, status updates)
- `src/FileSync.Core`: Core services, models, view models, LocalStack integration
- `src/FileSync.Interop.Native`: Native C++ CFAPI DLL prototype and exports
- `scripts`: helper scripts for context-menu registration and LocalStack setup

## Implemented Assignment Coverage

- Register sync root through native interop API
- Create placeholder files through native interop API
- Trigger hydration through native interop API
- Notify file state changes from C# to native engine
- Drag-and-drop upload to LocalStack S3 + local metadata persistence
- Manual hydration flow from WPF (**Hydrate Selected**)
- File listing UI with name, size, and sync status
- Status indicators: `Pending`, `Synced`, `Downloaded`, `Failed`
- Context menu registration script for Explorer upload command
- Exception handling + logging + user-friendly errors

## Dependencies (Windows)

Install these prerequisites to build and run the WPF app + native CFAPI interop prototype:

- Visual Studio 2022
  - Install via **Visual Studio Installer**
  - Required workloads:
    - `.NET desktop development`
    - `Desktop development with C++`
  - https://visualstudio.microsoft.com/vs/
- .NET SDK 6+ (or newer)
  - https://dotnet.microsoft.com/en-us/download/dotnet/6.0
- CMake (optional)
  - Only needed if you build the native component via CMake.
  - https://cmake.org/download/
- Docker Desktop
  - Used to run LocalStack locally.
  - https://www.docker.com/products/docker-desktop/
- AWS CLI v2
  - Used to create the S3 bucket inside LocalStack.
  - https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html

## How to Install Dependencies (Windows)

1. Install Visual Studio 2022 and ensure the workloads `.NET desktop development` and `Desktop development with C++` are checked.
2. Install .NET SDK 6+:
   - Download: https://dotnet.microsoft.com/en-us/download/dotnet/6.0
   - Optional (winget): `winget install Microsoft.DotNet.SDK.6`
3. Install and start Docker Desktop (required to run LocalStack):
   - https://www.docker.com/products/docker-desktop/
4. Install AWS CLI v2:
   - https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html
   - Optional (winget): `winget install Amazon.AWSCLI`
5. Install CMake (optional, only if you build the native component via CMake):
   - https://cmake.org/download/
   - Optional (winget): `winget install Kitware.CMake`

## Run Steps

1. Start LocalStack (and create bucket):
   - Using the helper script (PowerShell, from repo root):
     - `powershell -ExecutionPolicy Bypass -File scripts/setup-localstack.ps1 -Bucket cfapi-files`
   - Or manually:
     - Start LocalStack: `docker run -d --name localstack -p 4566:4566 localstack/localstack`
     - Create bucket: `aws --endpoint-url=http://localhost:4566 s3 mb s3://cfapi-files`
2. Open `FileSyncPrototype.sln` in Visual Studio.
3. Build `FileSyncInteropNative` (x64), then set `FileSync.App` as startup project.
4. Build and run `FileSync.App`.
5. In app, set sync root path and click **Initialize Sync Root**.
6. Drag files into app to upload and create placeholders.
7. Select an item and click **Hydrate Selected** to trigger download/hydration.

## Notes

- CFAPI callbacks are provided as a minimal prototype shape focused on assignment architecture.
- Some CFAPI operations require running on Windows with proper permissions and a valid sync root folder.
