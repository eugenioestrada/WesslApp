# Wessl

Wessl is a lightweight native Windows system-tray app for WSL Containers (WSLc), built with Microsoft.UI.Reactor and WinUI 3. It starts in the tray: left-click the icon to open the container window beside the tray, or right-click for the native WinUI Open and Exit menu.

This is an initial scaffold. The app uses sample containers by default and does not change real containers. `WslcClient` provides an experimental read-only CLI integration; to use it, replace the `SampleWslcClient` registration in `src/Wessl.App/Program.cs` with `WslcClient`. It calls `wslc container list --all --format json` and expects one JSON object per line. Verify that your WSLc version supports this format before enabling it.

## Prerequisites

- Windows 10 version 2004 (build 19041) or later, x64 or ARM64.
- .NET 10 SDK and the Windows SDK / WinUI build tools (for example, Visual Studio with the .NET desktop development and Windows application development workloads).
- WSL with WSL Containers and `wslc` on `PATH` only when using the real client. See the [WSL Containers guide](https://learn.microsoft.com/en-us/windows/wsl/tutorials/wsl-containers).

## Build and run

From a Windows developer terminal at the repository root:

```powershell
dotnet restore Wessl.slnx
dotnet build Wessl.slnx -p:Platform=x64
dotnet run --project src/Wessl.App/Wessl.App.csproj -p:Platform=x64
dotnet test tests/Wessl.Tests/Wessl.Tests.csproj
```

The repository's `NuGet.Config` restores packages from nuget.org, independently of any additional package sources configured on your machine.

For ARM64, use `-p:Platform=ARM64` instead. The app is unpackaged and includes the Windows App SDK runtime. Building the WinUI app requires Windows; domain and infrastructure tests can run on any platform supported by .NET 10.

## NativeAOT publish

NativeAOT publishes are available for unpackaged Windows x64 and ARM64 builds. Run the following commands from Windows with the .NET 10 SDK and Visual C++ Build Tools (Desktop development with C++) installed:

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
