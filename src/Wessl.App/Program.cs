using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Reactor;
using Wessl.App.Services;
using Wessl.Domain;
using Wessl.Infrastructure;

using var services = new ServiceCollection()
    .AddSingleton<IWslcClient, SampleWslcClient>()
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
        }))
    .AddSingleton<ContainerPolling>()
    .AddSingleton<AppLifecycle>()
    .BuildServiceProvider();

ReactorApp.Run(_ => services.GetRequiredService<AppLifecycle>().Start());
