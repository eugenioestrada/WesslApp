using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Reactor;
using Wessl.App.Services;
using Wessl.Domain;
using Wessl.Infrastructure;

using var services = new ServiceCollection()
    .AddWslcClient(useSampleClient: Environment.GetEnvironmentVariable("WESSL_USE_SAMPLE_WSLC") == "1")
    .AddSingleton<IContainerLogsClient>(provider =>
        (IContainerLogsClient)provider.GetRequiredService<IWslcClient>())
    .AddSingleton<IContainerShellLauncher, WindowsTerminalShellLauncher>()
    .AddSingleton<IStartupManager, WindowsStartupManager>()
    .AddSingleton<ContainerService>(provider => new ContainerService(
        provider.GetRequiredService<IWslcClient>(),
        action =>
        {
            var dispatcher = ReactorApp.UIDispatcher
                ?? throw new InvalidOperationException("The UI dispatcher is not available.");
            if (!dispatcher.TryEnqueue(() => action()))
            {
                throw new InvalidOperationException("Could not dispatch the container refresh result.");
            }
        },
        provider.GetRequiredService<IContainerShellLauncher>(),
        provider.GetRequiredService<AppSettings>()))
    .AddSingleton<ContainerPolling>()
    .AddSingleton<ImageService>(provider => new ImageService(
        provider.GetRequiredService<IImageClient>(),
        action =>
        {
            var dispatcher = ReactorApp.UIDispatcher
                ?? throw new InvalidOperationException("The UI dispatcher is not available.");
            if (!dispatcher.TryEnqueue(() => action()))
            {
                throw new InvalidOperationException("Could not dispatch the image result.");
            }
        }))
    .AddSingleton<ContainerDetailsService>(provider => new ContainerDetailsService(
        provider.GetRequiredService<IWslcClient>(),
        action =>
        {
            var dispatcher = ReactorApp.UIDispatcher
                ?? throw new InvalidOperationException("The UI dispatcher is not available.");
            if (!dispatcher.TryEnqueue(() => action()))
            {
                throw new InvalidOperationException("Could not dispatch the container details result.");
            }
        }))
    .AddSingleton<AppSettings>()
    .AddSingleton<AppLifecycle>()
    .BuildServiceProvider();

ReactorApp.Run(_ => services.GetRequiredService<AppLifecycle>().Start());
