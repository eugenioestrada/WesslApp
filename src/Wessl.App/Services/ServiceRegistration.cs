using Microsoft.Extensions.DependencyInjection;
using Wessl.Domain;
using Wessl.Infrastructure;

namespace Wessl.App.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddWslcClient(this IServiceCollection services) =>
        services.AddSingleton<IWslcClient, WslcClient>();
}
