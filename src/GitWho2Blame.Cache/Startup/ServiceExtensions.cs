using GitWho2Blame.Cache.Abstractions;
using GitWho2Blame.Cache.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GitWho2Blame.Cache.Startup;

public static class ServiceExtensions
{
    public static IServiceCollection AddCacheServices(this IServiceCollection services)
    {
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 100 * 1024 * 1024; // 100 MB limit
        });
        
        services.AddSingleton<ICacheService, MemoryCacheService>();
        return services;
    }
}