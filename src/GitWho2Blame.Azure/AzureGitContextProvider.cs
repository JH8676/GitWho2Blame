using GitWho2Blame.Azure.Options;
using GitWho2Blame.MCP.Abstractions;
using GitWho2Blame.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace GitWho2Blame.Azure;

public class AzureGitContextProvider : IGitContextProvider
{
    private readonly ILogger<AzureGitContextProvider> _logger;
    private readonly AzureGitOptions _options;
    private readonly GitHttpClient _client;
    
    public AzureGitContextProvider(
        ILogger<AzureGitContextProvider> logger,
        IOptions<AzureGitOptions> options,
        IVssConnection connection)
    {
        _logger = logger;
        _options = options.Value;
        _client = connection.GetClient<GitHttpClient>();
    }
    
    // TODO only seems to get prs and not commits
    public async Task<List<CodeChangeSummary>> GetCodeChangesAsync(
        string relativeFilePath,
        string repoName,
        string owner,
        int startLine,
        int endLine,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        //TODO: caching, this doesnt change much
        var repositories = await _client.GetRepositoriesAsync(
            _options.ProjectId,
            cancellationToken: cancellationToken);
        
        var repository = repositories.FirstOrDefault(r => r.Name.Equals(repoName, StringComparison.OrdinalIgnoreCase));
        if (repository == null)
        {
            throw new ArgumentException($"Repository '{repoName}' not found in project '{_options.ProjectId}'.");
        }
        
        _logger.LogInformation("Found repository {RepositoryName} with ID {RepositoryId} in project {ProjectId}",
            repository.Name, repository.Id, _options.ProjectId);
        
        var commitSummaries = await _client.GetCommitsAsync(
            _options.ProjectId,
            repository.Id,
            new GitQueryCommitsCriteria
            {
                // TODO: here if fix for only getting PRs?
                // ItemVersion = new GitVersionDescriptor
                // {
                //     VersionType = GitVersionType.Branch,
                //     Version = owner // Assuming owner is the branch name, adjust as necessary
                // },
                ItemPath = relativeFilePath,
                FromDate = since.ToShortDateString(),
            },
            cancellationToken: cancellationToken);
        
        _logger.LogInformation("Found {CommitCount} commits for file {RelativeFilePath} in repository {RepoName} since {Since}",
            commitSummaries.Count, relativeFilePath, repoName, since);

        var changeSummaries = new List<CodeChangeSummary>();
        foreach (var commitSummary in commitSummaries)
        {
            var commit = await _client.GetCommitAsync(
                _options.ProjectId,
                commitSummary.CommitId,
                repository.Id,
                cancellationToken: cancellationToken);
            
            var parentCommitId = commit.Parents.FirstOrDefault();
            if (parentCommitId == null)
            {
                // TODO is this wrong, no parent commit all adds? should still return this
                _logger.LogWarning("Commit {CommitId} has no parent commits, skipping",
                    commitSummary.CommitId);
                continue;
            }
            
            var commitFileContent = await _client.GetItemContentAsync(
                _options.ProjectId,
                repository.Id,
                relativeFilePath,
                versionDescriptor: new GitVersionDescriptor()
                {
                    Version = commitSummary.CommitId,
                    VersionType = GitVersionType.Commit
                },
                cancellationToken: cancellationToken);
            
            var parentCommitFileContent = await _client.GetItemContentAsync(
                _options.ProjectId,
                repository.Id,
                relativeFilePath,
                versionDescriptor: new GitVersionDescriptor()
                {
                    Version = parentCommitId,
                    VersionType = GitVersionType.Commit
                },
                cancellationToken: cancellationToken);
            
            var diff = await _client.GetFileDiffsAsync(
                new FileDiffsCriteria()
                {
                    BaseVersionCommit = parentCommitId,
                    TargetVersionCommit = commitSummary.CommitId,
                    FileDiffParams =
                    [
                        new FileDiffParams
                        {
                            Path = relativeFilePath,
                            OriginalPath = relativeFilePath  // TODO handle file path changing?
                        }
                    ]
                },
                _options.ProjectId,
                repository.Id,
                
                cancellationToken: cancellationToken);

            var relevantChanges = diff
                .SelectMany(d => d.LineDiffBlocks.Where(ldb => 
                    (ldb.ModifiedLineNumberStart >= startLine || ldb.OriginalLineNumberStart >= startLine) &&
                    ldb.ChangeType != LineDiffBlockChangeType.None))
                .ToList();
            
            var currentChangedLinesByType = relevantChanges
                .GroupBy(ldb => ldb.ChangeType)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(ldb =>
                            Enumerable.Range(ldb.ModifiedLineNumberStart, ldb.ModifiedLinesCount))
                        .Distinct()
                        .ToArray()
                );
            
            var parentChangedLinesByType = relevantChanges
                .GroupBy(ldb => ldb.ChangeType)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(ldb =>
                            Enumerable.Range(ldb.OriginalLineNumberStart, ldb.OriginalLinesCount))
                        .Distinct()
                        .ToArray()
                );
            
            var changedLines = GetChangedLines(
                parentCommitFileContent,
                commitFileContent,
                startLine,
                endLine,
                currentChangedLinesByType,
                parentChangedLinesByType);

            if (changedLines.Length == 0)
            {
                _logger.LogInformation("No changed lines found for commit {CommitId} in file {RelativeFilePath}",
                    commitSummary.CommitId, relativeFilePath);
                continue;
            }
            
            var changeSummary = new CodeChangeSummary(
                commitSummary.CommitId,
                commit.Author.Name,
                commit.Comment,
                commit.Author.Date,
                changedLines);

            changeSummaries.Add(changeSummary);
        }

        return changeSummaries;
    }
    
    private static CodeLine[] GetChangedLines(
        Stream parentStream, 
        Stream currentStream, 
        int startLine, 
        int endLine,
        Dictionary<LineDiffBlockChangeType, int[]> currentChangedLinesByType,
        Dictionary<LineDiffBlockChangeType, int[]> parentChangedLinesByType)
    {
        var parent = ParseContent(
            parentStream, 
            startLine, 
            endLine, 
            parentChangedLinesByType,
            (changeType, lineNumber, line) =>
            {
                return changeType switch
                {
                    LineDiffBlockChangeType.Delete or LineDiffBlockChangeType.Edit => CodeLine.Delete(lineNumber, line),
                    _ => null
                };
            });
        
        var current = ParseContent(
            currentStream, 
            startLine, 
            endLine, 
            currentChangedLinesByType,
            (changeType, lineNumber, line) =>
            {
                return changeType switch
                {
                    LineDiffBlockChangeType.Add or LineDiffBlockChangeType.Edit => CodeLine.Add(lineNumber, line),
                    _ => null
                };
            });
        
        var mergedLines = MergeChanges(parent, current);
        return mergedLines;
    }
    
    private static CodeLine[] ParseContent(
        Stream fileContent,
        int startLine,
        int endLine,
        Dictionary<LineDiffBlockChangeType, int[]> changedLineNumbersByType,
        Func<LineDiffBlockChangeType, int, string, CodeLine?> lineHandler)
    {
        var lines = new List<CodeLine>();
        using var reader = new StreamReader(fileContent);

        var currentLine = 1;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (currentLine >= startLine && currentLine <= endLine)
            {
                var change = changedLineNumbersByType
                    .FirstOrDefault(kvp => kvp.Value.Contains(currentLine));

                var codeLine = lineHandler(change.Key, currentLine, line ?? string.Empty);
                if (codeLine != null)
                {
                    lines.Add(codeLine);
                }
            }

            if (currentLine > endLine)
            {
                break;
            }

            currentLine++;
        }

        return lines.ToArray();
    }
    
    private static CodeLine[] MergeChanges(CodeLine[] parent, CodeLine[] current)
    {
        var result = new CodeLine[parent.Length + current.Length];
        int i = 0, j = 0, x = 0;

        while (i < parent.Length || j < current.Length)
        {
            int? parentLine = i < parent.Length ? parent[i].LineNumber : null;
            int? currentLine = j < current.Length ? current[j].LineNumber : null;
            
            if (parentLine.HasValue && (!currentLine.HasValue || parentLine.Value <= currentLine.Value))
            {
                AddConsecutiveLines(parent, ref result, ref i, ref x);
            }
            else
            {
                AddConsecutiveLines(current, ref result, ref j, ref x);
            }
        }

        return result.ToArray();
    }
    
    private static void AddConsecutiveLines(
        CodeLine[] lines,
        ref CodeLine[] result,
        ref int lineIndex,
        ref int resultIndex)
    {
        // Add the first line
        result[resultIndex++] = lines[lineIndex++];
        
        // Add consecutive lines after the first
        while (lineIndex < lines.Length && lines[lineIndex].LineNumber == lines[lineIndex-1].LineNumber + 1)
        {
            result[resultIndex++] = lines[lineIndex++];
        }
    }
}