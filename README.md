# Wessl

Wessl is a lightweight native Windows system-tray app for WSL Containers (WSLc), built with Microsoft.UI.Reactor and WinUI 3. It starts in the tray: left-click the icon to toggle the container window, or right-click for Open, Refresh, and Exit.

This is an initial scaffold. The app uses sample containers by default and does not change real containers. `WslcClient` provides an experimental read-only CLI integration; to use it, replace the `SampleWslcClient` registration in `src/Wessl.App/Program.cs` with `WslcClient`. It calls `wslc container list --all --format json` and expects one JSON object per line. Verify that your WSLc version supports this format before enabling it.

## Prerequisites

- Windows 10 version 2004 (build 19041) or later, x64 or ARM64.
- .NET 11 SDK and the Windows SDK / WinUI build tools (for example, Visual Studio with the .NET desktop development and Windows application development workloads).
- WSL with WSL Containers and `wslc` on `PATH` only when using the real client. See the [WSL Containers guide](https://learn.microsoft.com/en-us/windows/wsl/tutorials/wsl-containers).

## Build and run

From a Windows developer terminal at the repository root:

```powershell
dotnet restore Wessl.slnx
dotnet build Wessl.slnx
dotnet run --project src/Wessl.App/Wessl.App.csproj
dotnet test tests/Wessl.Tests/Wessl.Tests.csproj
```

For ARM64, pass `-p:RuntimeIdentifier=win-arm64` to the restore, build, and run commands instead of the default `win-x64`. The app is unpackaged and includes the Windows App SDK runtime. Building the WinUI app requires Windows; domain and infrastructure tests can run on any platform supported by .NET 11.

## Layout

- `src/Wessl.Domain`: dependency-free container types and client interface.
- `src/Wessl.Infrastructure`: sample client and asynchronous WSLc process/JSON parser.
- `src/Wessl.App`: Reactor components, tray lifecycle, and dependency injection.
- `tests/Wessl.Tests`: domain and parser tests.
