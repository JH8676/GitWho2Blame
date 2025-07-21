using GitWho2Blame.Common.Helpers;
using GitWho2Blame.MCP.Abstractions;
using GitWho2Blame.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace GitWho2Blame.Git;

public class LocalGitContextProvider(ILogger<LocalGitContextProvider> logger) : IGitContextProvider
{
    public Task<List<CodeChangeSummary>> GetCodeChangesAsync(
        string relativeFilePath,
        string repoRootPath,
        string repoName, 
        string owner, 
        string currentBranchName,
        int startLine,
        int endLine, 
        DateTime since, 
        CancellationToken cancellationToken = default)
    {
        var result = new List<CodeChangeSummary>();
        var repoPath = Repository.Discover(repoRootPath);
        if (repoPath == null)
        {
            logger.LogWarning("Could not find repository at path {RepoRootPath}", repoRootPath);
            return Task.FromResult(result);
        }

        using var repo = new Repository(repoPath);

        var filter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time,
            IncludeReachableFrom = repo.Head,
            FirstParentOnly = true,
        };

        var commits = repo.Commits.QueryBy(relativeFilePath, filter)
            .Where(le => le.Commit.Committer.When > since)
            .Select(le => le.Commit)
            .ToList();
        
        if  (commits.Count == 0)
        {
            logger.LogInformation("No commits found for file {RelativeFilePath} in repository {RepoName} since {Since}",
                relativeFilePath,
                repoName,
                since);
            
            return Task.FromResult(result);
        }
        
        
        foreach (var commit in commits)
        {
            
            var parent = commit.Parents.FirstOrDefault();
            var tree = commit.Tree;
            var parentTree = parent?.Tree;

            using var patch = repo.Diff.Compare<Patch>(parentTree, tree);

            var filePatch = patch[relativeFilePath];
            if (filePatch is null || filePatch.IsBinaryComparison)
            {
                continue;
            }
            
            var linesChanged = GitDiffParser.ParsePatch(filePatch.Patch, startLine, endLine);
            if (linesChanged.Count == 0)
            {
               logger.LogInformation("No lines changed in file {RelativeFilePath} for commit {Sha}", relativeFilePath, commit.Sha);
                continue;
            }
            
            var codeChangeSummary = new CodeChangeSummary(
                commit.Sha,
                commit.Author.Name,
                commit.MessageShort,
                commit.Author.When,
                linesChanged.ToArray()
            );
            
            result.Add(codeChangeSummary);
        }

        return Task.FromResult(result);
    }
}