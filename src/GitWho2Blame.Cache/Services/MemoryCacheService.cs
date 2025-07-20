using System.Text;
using System.Text.Json;
using GitWho2Blame.Cache.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace GitWho2Blame.Cache.Services;

public class MemoryCacheService(IMemoryCache cache) : ICacheService
{
    public Task<T?> GetOrAddAsync<T>(string key, Func<Task<T?>> factory, TimeSpan ttl)
        => cache.GetOrCreateAsync(key, async entry =>
        {
            var result = await factory();
            entry.AbsoluteExpirationRelativeToNow = ttl;
            entry.Size = GetSize(result); 
            return result;
        });

    private static long GetSize<T>(T? value)
        => value switch
        {
            null => 0,
            byte[] bytes => bytes.Length,
            string s => Encoding.UTF8.GetByteCount(s),
            _ => JsonSerializer.SerializeToUtf8Bytes(value).Length,
        };
}