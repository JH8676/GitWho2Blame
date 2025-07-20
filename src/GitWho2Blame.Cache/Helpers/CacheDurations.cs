namespace GitWho2Blame.Cache.Helpers;

public static class CacheDurations
{
    /// <summary>
    /// A short cache duration, suitable for data that changes frequently. (e.g., 5 minutes)
    /// </summary>
    public static readonly TimeSpan Short = TimeSpan.FromMinutes(5);

    /// <summary>
    /// A medium cache duration, suitable for semi-static data. (e.g., 1 hour)
    /// </summary>
    public static readonly TimeSpan Medium = TimeSpan.FromHours(1);

    /// <summary>
    /// A long cache duration, suitable for data that rarely changes. (e.g., 6 hours)
    /// </summary>
    public static readonly TimeSpan Long = TimeSpan.FromHours(6);
}