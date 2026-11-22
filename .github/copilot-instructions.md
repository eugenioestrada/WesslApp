# Wessl architecture

- Keep `Wessl.Domain` independent of external packages and infrastructure; use POCOs and interfaces.
- Write UI declaratively in C# with Microsoft.UI.Reactor. Do not introduce XAML or MVVM/ViewModels.
- Keep components small and composable. Put lifecycle and WSLc integration in services.
- Prefer built-in .NET and Reactor features; avoid unnecessary third-party libraries.

# .NET 10 to .NET 11 migrations

- .NET 11 is GA (STS). Verify applicable changes against the official .NET 11 compatibility and release notes.
- Update dependencies only when required or when a matching supported release is appropriate; avoid blanket upgrades.
- Projects are already SDK-style. Do not enable NativeAOT or change NuGet feed or restore configuration unless explicitly requested.
- Validate the Windows app for both `win-x64` and `win-arm64` when the environment permits.
