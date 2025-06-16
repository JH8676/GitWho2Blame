using System.Text.RegularExpressions;

namespace GitWho2Blame.GitHub.Helpers;

public static partial class RegexHelpers
{
    [GeneratedRegex(@"@@ -(\d+),?(\d*) \+(\d+),?(\d*) @@")]
    public static partial Regex GitHubHunkHeaderRegex();
}