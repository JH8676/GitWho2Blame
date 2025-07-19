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
    private const string EndOfFileMarker = @"\ No newline at end of file";
    
    private readonly ILogger<GitHubContextProvider> _logger;
    private readonly GitHubClient _client;
    
    public GitHubContextProvider(
        ILogger<GitHubContextProvider> logger,
        IOptions<GitHubOptions> options)
    {
        _logger = logger;
        _client = new GitHubClient(new ProductHeaderValue(nameof(GitWho2Blame).ToLower()))
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
            var linesChanged = new List<CodeLine>();
            
            // TODO: improve this logic
            for (var i = 0; i < patchLines.Length; i++)
            {
                var unchangedLineCount = 0;
                var addedLineCount = 0;
                var deletedLineCount = 0;
                
                var line = patchLines[i];
                
                var hunkHeaderMatch = RegexHelpers.GitHubHunkHeaderRegex().Match(line);
                if (!hunkHeaderMatch.Success)
                {
                    continue;
                }
               
                var originalStart = int.Parse(hunkHeaderMatch.Groups[1].Value);
                var newStart = int.Parse(hunkHeaderMatch.Groups[3].Value);
                var newCount = string.IsNullOrEmpty(hunkHeaderMatch.Groups[4].Value) ? 1 : int.Parse(hunkHeaderMatch.Groups[4].Value);
                var newEnd = newStart + newCount - 1;

                if (!(newEnd >= startLine && newStart <= endLine))
                {
                    continue;
                }

                for (var j = i + 1; j < patchLines.Length; j++)
                {
                    line = patchLines[j];
                    
                    hunkHeaderMatch = RegexHelpers.GitHubHunkHeaderRegex().Match(line);
                    if (hunkHeaderMatch.Success)
                    {
                        i = j - 1;
                        break;
                    }
                    
                    if (line == EndOfFileMarker)
                    {
                        // Skip the end of file marker
                        continue;
                    }
                    
                    switch (line[0])
                    {
                        case '+':
                            var newLineNumber = newStart + addedLineCount + unchangedLineCount;
                            linesChanged.Add(CodeLine.Add(newLineNumber, line));
                            
                            addedLineCount++;
                            break;
                        case '-':
                            var originalLineNumber = originalStart + deletedLineCount + unchangedLineCount;
                            linesChanged.Add(CodeLine.Delete(originalLineNumber, line));
                            
                            deletedLineCount++;
                            break;
                
                        default:
                            unchangedLineCount++;
                            break;
                    }
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