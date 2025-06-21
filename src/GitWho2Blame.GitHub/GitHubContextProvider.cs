using GitWho2Blame.GitHub.Helpers;
using GitWho2Blame.GitHub.Options;
using GitWho2Blame.MCP.Abstractions;
using GitWho2Blame.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace GitWho2Blame.GitHub;

public class GitHubContextProvider : IGitContextProvider
{
    private readonly ILogger<GitHubContextProvider> _logger;
    private readonly GitHubClient _client;
    
    public GitHubContextProvider(
        ILogger<GitHubContextProvider> logger,
        IOptions<GitHubOptions> options)
    {
        _logger = logger;
        _client = new GitHubClient(new ProductHeaderValue("gitwho2blame"))
        {
            Credentials = new Credentials(options.Value.Token)
        };
    }
    
    public async Task<List<CodeChangeSummary>> GetCodeChangesAsync(
        string relativeFilePath,
        string repoName,
        string owner,
        int startLine,
        int endLine,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        var commitSummaries = await _client.Repository.Commit.GetAll(
            owner,
            repoName, 
            new CommitRequest
            {
                Since = since,
                Path = relativeFilePath
            });
        
        _logger.LogInformation("Found {CommitCount} commits for file {RelativeFilePath} in repository {RepoName} since {Since}",
            commitSummaries.Count,
            relativeFilePath,
            repoName,
            since);
            
        var result = new List<CodeChangeSummary>();

        foreach (var commitSummary in commitSummaries)
        {
            var commit = await _client.Repository.Commit.Get(owner, repoName, commitSummary.Sha);
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
            
            var patchLines = file.Patch.Split(Environment.NewLine);
            var linesChanged = new List<string>();
            
            var inRelevantHunk = false;
            foreach (var line in patchLines)
            {
                var hunkHeaderMatch = RegexHelpers.GitHubHunkHeaderRegex().Match(line);
                if (hunkHeaderMatch.Success)
                {
                    var newStart = int.Parse(hunkHeaderMatch.Groups[3].Value);
                    var newCount = string.IsNullOrEmpty(hunkHeaderMatch.Groups[4].Value) ? 1 : int.Parse(hunkHeaderMatch.Groups[4].Value);
                    var newEnd = newStart + newCount - 1;

                    inRelevantHunk = newEnd >= startLine && newStart <= endLine;
                    continue; // Skip the hunk header itself
                }

                if (inRelevantHunk && (line.StartsWith('+') || line.StartsWith('-')))
                {
                    linesChanged.Add(line);
                }
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