using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Reactor;
using Wessl.App.Services;
using Wessl.Domain;
using Wessl.Infrastructure;

using var services = new ServiceCollection()
    .AddSingleton<IWslcClient, WslcClient>()
    .AddSingleton<ContainerService>()
    .AddSingleton<AppLifecycle>()
    .BuildServiceProvider();

ReactorApp.Run(_ => services.GetRequiredService<AppLifecycle>().Start());
