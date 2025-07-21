using GitWho2Blame.Cache.Abstractions;
using GitWho2Blame.Cache.Helpers;
using GitWho2Blame.Common.Helpers;
using GitWho2Blame.MCP.Abstractions;
using GitWho2Blame.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace GitWho2Blame.GitHub;

public class GitHubContextProvider : IGitContextProvider
{
    private const string GitHub = "github";
    
    private readonly ILogger<GitHubContextProvider> _logger;
    private readonly IGitHubClient _client;
    private readonly ICacheService _cache;

    public GitHubContextProvider(
        ILogger<GitHubContextProvider> logger,
        IGitHubClient client,
        ICacheService cache)
    {
        _logger = logger;
        _client = client;
        _cache = cache;
    }
    
    public async Task<List<CodeChangeSummary>> GetCodeChangesAsync(
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
        var commitSummaries = await _cache.GetOrAddAsync(
            CacheKeyGenerator.GenerateKey(GitHub, CacheKeyParts.Commits, owner, repoName, relativeFilePath, since),
            async () => await _client.Repository.Commit.GetAll(owner, repoName, new CommitRequest
            {
                Since = since,
                Path = relativeFilePath
            }),
            CacheDurations.Short);

        if (commitSummaries is null || !commitSummaries.Any())
        {
            _logger.LogInformation("No commits found for file {RelativeFilePath} in repository {RepoName} since {Since}",
                relativeFilePath,
                repoName,
                since);
            
            return [];
        }
        
        _logger.LogInformation("Found {CommitCount} commits for file {RelativeFilePath} in repository {RepoName} since {Since}",
            commitSummaries.Count,
            relativeFilePath,
            repoName,
            since);
            
        var result = new List<CodeChangeSummary>();

        foreach (var commitSummary in commitSummaries)
        {
            var commit = await _cache.GetOrAddAsync(
                CacheKeyGenerator.GenerateKey(GitHub, CacheKeyParts.Commit, owner, repoName, commitSummary.Sha),
                async () => await _client.Repository.Commit.Get(owner, repoName, commitSummary.Sha),
                CacheDurations.Long);

            if (commit is null)
            {
                _logger.LogWarning("Commit {Sha} not found in repository {RepoName}, skipping",
                    commitSummary.Sha,
                    repoName);
                
                continue;
            }
                
            _logger.LogInformation("Processing commit {Sha} with {FileCount} files",
                commitSummary.Sha,
                commit.Files.Count);
            
            var file = commit.Files
                .FirstOrDefault(f => f.Filename.Equals(relativeFilePath, StringComparison.OrdinalIgnoreCase));
            
            if (file == null)
            {
                _logger.LogWarning("File {RelativeFilePath} not found in commit {Sha}, skipping",
                    relativeFilePath,
                    commitSummary.Sha);
                continue;
            }
            
            var linesChanged = GitDiffParser.ParsePatch(file.Patch, startLine, endLine);

            if (linesChanged.Count == 0)
            {
                _logger.LogInformation("No lines changed in file {RelativeFilePath} for commit {Sha}, skipping",
                    relativeFilePath,
                    commitSummary.Sha);
                
                continue;
            }
            
            var codeChangeSummary = new CodeChangeSummary(
                commitSummary.Sha,
                commitSummary.Commit.Author.Name,
                commitSummary.Commit.Message,
                commitSummary.Commit.Author.Date,
                linesChanged.ToArray());
            
            result.Add(codeChangeSummary);
        }
        
        return result;
    }
    
}