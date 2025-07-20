namespace GitWho2Blame.Cache.Abstractions;

public interface ICacheService
{
    Task<T?> GetOrAddAsync<T>(string key, Func<Task<T?>> factory, TimeSpan ttl); 
}