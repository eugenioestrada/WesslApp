# Wessl

Wessl is a lightweight native Windows system-tray app for WSL Containers (WSLc), built with Microsoft.UI.Reactor and WinUI 3. It starts in the tray: left-click the icon to open the container window beside the tray, or right-click for the native WinUI Open and Exit menu.

See the [roadmap](ROADMAP.md) for planned milestones and feature priorities.

The app manages real containers using `wslc` on `PATH`; its list command must support `wslc container list --all --format json` with one JSON object per line. If WSLc cannot be started, the app offers instructions to update WSL and a button to copy `wsl --update`. Start, Stop, and Restart run directly from each row; Remove requires confirmation and can stop a running container as part of deletion.

Published TCP mappings in a container's `Ports` field also offer localhost URLs to open or copy.

For a running container, **Open shell** launches Windows Terminal with an interactive `wslc exec -it <container-id> /bin/sh` session. Windows Terminal and a WSLc version supporting `exec -it` must be available.

Container status refreshes when the window opens and every 10 seconds while it is visible. The Refresh button also requests an immediate update. Polling stops when the window is hidden, minimized, or closed; starting the tray app alone does not invoke WSLc. The tray indicator shows unknown until the first successful refresh, then reflects the last known status while the window is closed.

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

## Localization

The WinUI app uses Windows App SDK MRT Core `.resw` resources in `src/Wessl.App/Strings/<BCP-47>/Resources.resw`. English (`en-US`) is the default language and Spanish (`es-ES`) is included. Add translations in a new language folder with the same resource keys; the Windows App SDK build indexes these files into the app's PRI resources. Because the app is unpackaged, Windows selects translations using the system display language rather than the user's preferred language list.

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

## Release distribution

The GitHub Actions workflows build and test pull requests and pushes to `main`. Pushing a version tag such as `v1.2.3` starts the release workflow, which publishes NativeAOT archives for `win-x64` and `win-arm64` and creates a GitHub Release with generated notes. The workflow rejects tags that do not use `vMAJOR.MINOR.PATCH` (an optional prerelease suffix is supported), refuses to replace an existing release, and uses the repository-provided `GITHUB_TOKEN` with write access only in the release job.

The release also includes a generated set of winget manifests with the exact archive SHA-256 values. The workflow does not submit packages or manifests to the winget community repository; that submission is a separate maintainer action after reviewing the release assets. Once the package has been accepted into winget, users can install it with:

```powershell
winget install --id eugenioestrada.Wessl --exact
```

Each archive contains the full unpackaged publish output. The winget manifest registers `wessl.exe` as a portable command alias; Wessl can also be started directly from its extracted folder.

## Layout

- `src/Wessl.Domain`: dependency-free container types and client interface.
- `src/Wessl.Infrastructure`: sample client and asynchronous WSLc process/JSON parser.
- `src/Wessl.App`: Reactor components, tray lifecycle, and dependency injection.
- `tests/Wessl.Tests`: domain and parser tests.
