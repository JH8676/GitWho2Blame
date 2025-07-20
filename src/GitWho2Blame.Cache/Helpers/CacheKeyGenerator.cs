namespace GitWho2Blame.Cache.Helpers;

public static class CacheKeyGenerator
{
    private const string DateTimeFormat = "yyyyMMddHHmmss";

    public static string GenerateKey(params object[] parts)
        => string.Join(":", parts.Select(part => FormatPart(part).Trim()));

    private static string FormatPart(object part)
    {
        return part switch
        {
            DateTime dt => dt.ToString(DateTimeFormat),
            _ => part.ToString() ?? string.Empty
        };
    }
}