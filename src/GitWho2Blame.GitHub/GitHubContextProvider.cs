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
    private readonly IGitHubClient _client;
    
    public GitHubContextProvider(
        ILogger<GitHubContextProvider> logger,
        IGitHubClient client)
    {
        _logger = logger;
        _client = client;
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
                var line = patchLines[i];
                
                var hunkHeaderMatch = RegexHelpers.GitHubHunkHeaderRegex().Match(line);
                if (!hunkHeaderMatch.Success)
                {
                    continue;
                }

                i++;
               
                var originalStart = int.Parse(hunkHeaderMatch.Groups[1].Value);
                var newStart = int.Parse(hunkHeaderMatch.Groups[3].Value);
                var newCount = string.IsNullOrEmpty(hunkHeaderMatch.Groups[4].Value) ? 1 : int.Parse(hunkHeaderMatch.Groups[4].Value);
                var newEnd = newStart + newCount - 1;

                if (!(newEnd >= startLine || newStart <= endLine))
                {
                    continue;
                }
                
                var linesChangedInHunk = GetLineChanges(ref i, newStart, originalStart, patchLines);
                linesChanged.AddRange(linesChangedInHunk);
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

    private static List<CodeLine> GetLineChanges(ref int index, int newStart, int originalStart, string[] patchLines)
    {
        var linesChanged = new List<CodeLine>();
        var unchangedLineCount = 0;
        var addedLineCount = 0;
        var deletedLineCount = 0;
        
        while (index < patchLines.Length)
        {
            var line = patchLines[index];

            if (RegexHelpers.GitHubHunkHeaderRegex().Match(line).Success)
            {
                index--;
                break;
            }
            
            if (line == EndOfFileMarker)
            {
                // Skip the end of file marker
                index++;
                continue;
            }
                    
            switch (line[0])
            {
                case '+':
                    var newLineNumber = newStart + addedLineCount + unchangedLineCount;
                    linesChanged.Add(new CodeLine
                    {
                        LineNumber = newLineNumber,
                        Content = line
                    });
                            
                    addedLineCount++;
                    break;
                case '-':
                    var originalLineNumber = originalStart + deletedLineCount + unchangedLineCount;
                    linesChanged.Add(new CodeLine
                    {
                        LineNumber = originalLineNumber,
                        Content = line
                    });
                            
                    deletedLineCount++;
                    break;
                
                default:
                    unchangedLineCount++;
                    break;
            }
            
            index++;
        }
        
        return linesChanged;
    }
}