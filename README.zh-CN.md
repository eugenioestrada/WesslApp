# Wessl

[English](README.md) · [Español](README.es-ES.md) · [简体中文](README.zh-CN.md) · [Français](README.fr-FR.md) · [Italiano](README.it-IT.md) · [Deutsch](README.de-DE.md)

Wessl 是一款轻量级的 Windows 原生系统托盘应用，用于管理 WSL Containers（WSLc），基于 Microsoft.UI.Reactor 和 WinUI 3 构建。应用启动后驻留在托盘中：左键单击图标可在托盘旁打开容器窗口，右键单击可显示原生 WinUI“打开”和“退出”菜单。

从托盘打开应用时，容器面板会以简短的淡入动画显示并获得焦点。当焦点移至其他应用或工具窗口时，面板会淡出并隐藏，应用仍继续驻留在托盘中。重新打开面板会取消尚未完成的淡出操作。面板不会始终置顶，过渡动画会遵循 Windows 动画设置。

有关计划中的里程碑和功能优先级，请参阅[路线图](ROADMAP.md)。

## 为什么叫“Wessl”？

**Wessl** 是 **WSL** 和 **Vessel** 的组合词。Vessel 既指装载集装箱的船，也指容器本身。这个名字保留了容器生态的航海意象，同时避免常见的陈词滥调。它的读音类似 *vessel*，但以 *W* 开头，体现了应用简洁、清晰、轻量的设计理念。

应用默认使用真实的 WSLc 客户端。`wslc` 可执行文件必须位于 `PATH` 中，其列表命令必须支持 `wslc container list --all --format json`，并且每行输出一个 JSON 对象。如果无法启动 WSLc，应用会显示更新 WSL 的说明以及复制 `wsl --update` 的按钮，不会回退到示例数据。若要在演示时使用示例容器和镜像数据，请在启动应用前设置 `WESSL_USE_SAMPLE_WSLC=1`。可以直接在每个容器行中启动、停止或重启容器；删除容器前需要确认，若容器正在运行，删除操作会先将其停止。

容器 `Ports` 字段中发布的 TCP 端口映射也会提供可打开或复制的 localhost URL。

打开**镜像**可显式加载 WSLc 的镜像列表及其报告的大小。刷新会重新加载列表。**清理未使用的镜像**需要确认，并会删除未被任何容器引用的所有镜像，包括带标签的镜像；界面会显示 WSLc 的结果和错误。镜像操作可能会启动 WSL，并且只有在用户操作后才会执行。

对于正在运行的容器，**打开终端**会启动 Windows Terminal，并运行交互式会话 `wslc exec -it <container-id> /bin/sh`。此操作需要使用真实的 WSLc 集成；系统中必须安装 Windows Terminal 和支持 `exec -it` 的 WSLc 版本。

**查看日志**会打开独立窗口并持续跟踪 `wslc container logs --timestamps -f <container-id>`。现有日志行和新流入的日志行均包含时间戳。可以搜索已加载的日志、复制所选或可见的日志行，或将可用日志保存到文件。关闭该窗口会停止跟踪进程。显示区域最多保留最近的 128 Ki 字符。

运行中的容器通过 `wslc stats --format json` 显示 CPU、内存、网络和块 I/O 以及 PID 数量。统计信息默认每秒刷新一次；可在设置中更改间隔。隐藏面板后停止轮询。

窗口打开时会刷新容器状态；窗口可见期间每 10 秒刷新一次。刷新按钮也可立即发起更新。窗口隐藏、最小化或关闭后会停止轮询；仅启动托盘应用不会调用 WSLc。托盘指示器在首次成功刷新前显示未知状态；窗口关闭后则反映最近一次已知状态。

## 环境要求

- Windows 10 版本 2004（内部版本 19041）或更高版本，x64 或 ARM64。
- .NET 11 SDK 和 Windows SDK / WinUI 构建工具（例如安装了“.NET 桌面开发”和“Windows 应用程序开发”工作负载的 Visual Studio）。
- 若要使用真实集成：安装 WSL 和 WSL Containers，并将 `wslc` 加入 `PATH`。请参阅 [WSL Containers 指南](https://learn.microsoft.com/en-us/windows/wsl/tutorials/wsl-containers)。

## 构建和运行

在 Windows 开发者终端中，从仓库根目录运行：

```powershell
dotnet restore Wessl.slnx
dotnet build Wessl.slnx
dotnet run --project src/Wessl.App/Wessl.App.csproj
dotnet test tests/Wessl.Tests/Wessl.Tests.csproj
```

仓库中的 `NuGet.Config` 会从 nuget.org 还原包，不受计算机上其他已配置包源的影响。

对于 ARM64，请在还原、构建和运行命令中传入 `-p:RuntimeIdentifier=win-arm64`，替代默认的 `win-x64`。该应用未打包，并包含 Windows App SDK 运行时。构建 WinUI 应用需要 Windows；Domain 和 Infrastructure 测试可在任何受 .NET 11 支持的平台上运行。

## 本地化

WinUI 应用使用 Windows App SDK MRT Core `.resw` 资源，位置为 `src/Wessl.App/Strings/<BCP-47>/Resources.resw`。应用支持英语（`en-US`）、西班牙语（`es-ES`）、简体中文（`zh-CN`）、法语（`fr-FR`）、意大利语（`it-IT`）和德语（`de-DE`），可在设置中选择。添加语言翻译时，请创建包含相同资源键的新语言文件夹；Windows App SDK 构建会将这些文件编入应用的 PRI 资源。

## NativeAOT 发布

可为 Windows x64 和 ARM64 发布未打包的 NativeAOT 构建。在 Windows 上安装 .NET 11 SDK 和 Visual C++ Build Tools（含“使用 C++ 的桌面开发”工作负载）后，运行以下命令：

```powershell
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-x64-nativeaot
dotnet publish src/Wessl.App/Wessl.App.csproj -p:PublishProfile=win-arm64-nativeaot
```

生成的文件位于：

- `src/Wessl.App/bin/publish/win-x64-nativeaot/`
- `src/Wessl.App/bin/publish/win-arm64-nativeaot/`

每个文件都包含 `wessl.exe`。NativeAOT 构建通过 `InvariantGlobalization=true` 禁用全球化数据，并且不支持框架构建范围以外的反射或动态代码。应用目前避免使用高度依赖反射的代码，Release 构建也不会启用 `Reactor.DevtoolsSupport`。发布不会改变常规的构建或运行流程。

NativeAOT 发布必须面向相应的 Windows 架构。从 x64 Windows 交叉发布 ARM64 需要相应的 Visual C++ 交叉编译工具，且生成的程序不一定能在发布所用的计算机上运行。

## 版本分发

GitHub Actions 工作流会构建并测试拉取请求以及推送到 `main` 的更改。推送诸如 `v1.2.3` 的版本标签会启动发布工作流：为 `win-x64` 和 `win-arm64` 发布 NativeAOT 压缩包，并创建带有自动生成说明的 GitHub Release。工作流会拒绝不符合 `vMAJOR.MINOR.PATCH` 格式的标签（可选预发布后缀除外），不会替换现有 Release，并且仅在发布任务中使用具有写入权限的仓库 `GITHUB_TOKEN`。

Release 还包含自动生成的 winget 清单，其中记录了压缩包的准确 SHA-256 值。工作流不会将软件包或清单提交到 winget 社区仓库；维护者需要在审核发布文件后单独提交。软件包被 winget 接受后，用户可通过以下命令安装：

```powershell
winget install --id eugenioestrada.Wessl --exact
```

每个压缩包都包含完整的未打包发布输出。winget 清单将 `wessl.exe` 注册为便携式命令别名；也可以直接从解压后的文件夹启动 Wessl。

## 目录结构

- `src/Wessl.Domain`：无依赖的容器类型和客户端接口。
- `src/Wessl.Infrastructure`：示例客户端以及异步 WSLc 进程 / JSON 解析器。
- `src/Wessl.App`：Reactor 组件、托盘生命周期和依赖注入。
- `tests/Wessl.Tests`：Domain 和解析器测试。
