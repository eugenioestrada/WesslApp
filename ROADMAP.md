# Wessl Roadmap

## Guiding Principles

- Keep Wessl zero-bloat, with near-zero idle RAM and CPU usage.
- Deliver a native Windows look and feel with WinUI 3 via `Microsoft.UI.Reactor`.
- Optimize for fast, keyboard- and tray-first workflows.

## v0.1 — MVP (Immediate Control)

- [x] [Show a system tray icon with a subtle running/idle indicator](https://github.com/eugenioestrada/WesslApp/issues/5).
- [x] [Provide a quick flyout or minimal window listing container name, image, state, and exposed ports](https://github.com/eugenioestrada/WesslApp/issues/6).
- [x] [Support one-click Start, Stop, Restart, and Remove, with quick confirmation for Remove](https://github.com/eugenioestrada/WesslApp/issues/7).
- [x] [Open an interactive container shell in Windows Terminal using `exec`](https://github.com/eugenioestrada/WesslApp/issues/8).
- [x] [Let users click exposed ports to open them in a browser or copy their URL](https://github.com/eugenioestrada/WesslApp/issues/9).
- [x] [Poll on demand only while the window is open or when manually triggered; never wake WSL in the background](https://github.com/eugenioestrada/WesslApp/issues/10).

## v1.0 — Daily Comfort & Maintenance

- [x] [View container logs with `-f` in a minimal built-in window or a Windows Terminal tab](https://github.com/eugenioestrada/WesslApp/issues/11).
- [x] [List images, show disk size, and prune unused images](https://github.com/eugenioestrada/WesslApp/issues/12).
- [x] [Add batch tray actions such as Stop All](https://github.com/eugenioestrada/WesslApp/issues/13).
- [x] [Add minimal settings for startup, active refresh interval, and default terminal](https://github.com/eugenioestrada/WesslApp/issues/14).
- [x] [Distribute through winget and GitHub Releases for `win-x64` and `win-arm64`](https://github.com/eugenioestrada/WesslApp/issues/15).

## v1.x — Future / Under Consideration

- [ ] [Group containers by Compose project or label, with group Start and Stop actions](https://github.com/eugenioestrada/WesslApp/issues/16).
- [x] [Show container RAM and CPU usage on demand while the UI is open](https://github.com/eugenioestrada/WesslApp/issues/17).
- [ ] [Send native Windows Toast notifications when a container exits unexpectedly with a non-zero code](https://github.com/eugenioestrada/WesslApp/issues/18).
- [x] [Search, filter, and sort containers](https://github.com/eugenioestrada/WesslApp/issues/53).
- [x] [Show health and exit diagnostics](https://github.com/eugenioestrada/WesslApp/issues/54).
- [x] [Inspect container details safely](https://github.com/eugenioestrada/WesslApp/issues/55).
- [x] [Improve container log tools](https://github.com/eugenioestrada/WesslApp/issues/56).
- [ ] [Expand image management actions](https://github.com/eugenioestrada/WesslApp/issues/57).
- [x] [Add safe bulk container actions](https://github.com/eugenioestrada/WesslApp/issues/58).

## Non-Goals (Explicitly Out of Scope)

- Historical metrics, telemetry, or real-time performance graphs.
- Built-in file browsers, Dockerfile editors, or YAML editors.
- User accounts, cloud sync, marketplaces, or plugins/extensions.

Priorities may evolve based on `wslc` updates and real-world usage. This roadmap targets .NET 10; .NET 11 work is separate.
