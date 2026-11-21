# Wessl architecture

- Keep `Wessl.Domain` independent of external packages and infrastructure; use POCOs and interfaces.
- Write UI declaratively in C# with Microsoft.UI.Reactor. Do not introduce XAML or MVVM/ViewModels.
- Keep components small and composable. Put lifecycle and WSLc integration in services.
- Prefer built-in .NET and Reactor features; avoid unnecessary third-party libraries.
