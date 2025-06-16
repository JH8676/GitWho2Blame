using GitWho2Blame.Git.Helpers;
using GitWho2Blame.MCP.Abstractions;
using GitWho2Blame.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace GitWho2Blame.Git;

public class GitService(ILogger<GitService> logger) 
    : IGitService
{
    public List<CodeLineChange> GetBlameForLinesAsync(string relativeFilePath, string repoRootPath, int startLine, int endLine)
    {
        var result = new List<CodeLineChange>();
        
        var repoPath = Repository.Discover(repoRootPath);
        
        using var repo = new Repository(repoPath);
        var blame = repo.Blame(relativeFilePath);
        
        foreach (var hunk in blame)
        {
            for (var i = 0; i < hunk.LineCount; i++)
            {
                var line = hunk.FinalStartLineNumber + i;
                if (line >= startLine && line <= endLine)
                {
                    result.Add(new CodeLineChange
                    {
                        Line = line,
                        Author = hunk.FinalSignature.Name,
                        Email = hunk.FinalSignature.Email,
                        CommitSha = hunk.FinalCommit.Sha,
                        CommitDate = hunk.FinalCommit.Committer.When
                    });
                }
            }
        }

        return result;
    }
    
    public string? GetRepositoryOwner(string repoRootPath)
    {
        var repoPath = Repository.Discover(repoRootPath);
        using var repo = new Repository(repoPath);

        var url = repo.Network.Remotes.FirstOrDefault()?.Url;
        if (url == null)
        {
            return null;
        }
        
        var match = RegexHelpers.GitHubOwnerFromUrlRegex().Match(url);
        var owner = match.Success ? match.Groups[1].Value : "Unknown";
        
        logger.LogInformation("Repository owner extracted: {Owner}", owner);
        return owner;
    }
}