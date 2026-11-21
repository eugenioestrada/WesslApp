# Wessl

Wessl is a lightweight native Windows system-tray app for WSL Containers (WSLc), built with Microsoft.UI.Reactor and WinUI 3. It starts in the tray: left-click the icon to open the container window beside the tray, or right-click for the native WinUI Open and Exit menu.

See the [roadmap](ROADMAP.md) for planned milestones and feature priorities.

The app uses `WslcClient` to list and manage real containers through the `wslc` CLI. Start, Stop, and Restart run directly from each container row; Remove requires confirmation and can stop a running container as part of deletion. The CLI must be available on `PATH`, and its list command must support `wslc container list --all --format json` with one JSON object per line.

## Prerequisites

- Windows 10 version 2004 (build 19041) or later, x64 or ARM64.
- .NET 11 SDK and the Windows SDK / WinUI build tools (for example, Visual Studio with the .NET desktop development and Windows application development workloads).
- WSL with WSL Containers and `wslc` on `PATH`. See the [WSL Containers guide](https://learn.microsoft.com/en-us/windows/wsl/tutorials/wsl-containers).

## Build and run

From a Windows developer terminal at the repository root:

```powershell
dotnet restore Wessl.slnx
dotnet build Wessl.slnx
dotnet run --project src/Wessl.App/Wessl.App.csproj
dotnet test tests/Wessl.Tests/Wessl.Tests.csproj
```

The repository's `NuGet.Config` restores packages from nuget.org, independently of any additional package sources configured on your machine.

For ARM64, pass `-p:RuntimeIdentifier=win-arm64` to the restore, build, and run commands instead of the default `win-x64`. The app is unpackaged and includes the Windows App SDK runtime. Building the WinUI app requires Windows; domain and infrastructure tests can run on any platform supported by .NET 11.

## NativeAOT publish

NativeAOT publishes are available for unpackaged Windows x64 and ARM64 builds. Run the following commands from Windows with the .NET 11 SDK and Visual C++ Build Tools (Desktop development with C++) installed:

```powershell
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-x64-nativeaot
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-arm64-nativeaot
```

The artifacts are written to:

- `src/Wessl.App/bin/publish/win-x64-nativeaot/`
- `src/Wessl.App/bin/publish/win-arm64-nativeaot/`

Each artifact contains `wessl.exe`. NativeAOT builds disable globalization data via `InvariantGlobalization=true` and do not support reflection or dynamic code outside the framework surface that is rooted in the AOT build. The app currently avoids reflection-heavy code and `Reactor.DevtoolsSupport` is not enabled in Release builds. Publishing does not require a change to the normal build or run workflow.

NativeAOT publication requires targeting the matching Windows architecture. Cross-publishing ARM64 from x64 Windows requires the corresponding Visual C++ cross-compilation tools and is not expected to run on the publishing machine.

## Layout

- `src/Wessl.Domain`: dependency-free container types and client interface.
- `src/Wessl.Infrastructure`: sample client and asynchronous WSLc process/JSON parser.
- `src/Wessl.App`: Reactor components, tray lifecycle, and dependency injection.
- `tests/Wessl.Tests`: domain and parser tests.
