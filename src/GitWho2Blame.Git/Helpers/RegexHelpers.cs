using System.Text.RegularExpressions;

namespace GitWho2Blame.Git.Helpers;

public static partial class RegexHelpers
{
    [GeneratedRegex(@"github\.com/([^/]+)/", RegexOptions.IgnoreCase)]
    public static partial Regex GitHubOwnerFromUrlRegex();
}