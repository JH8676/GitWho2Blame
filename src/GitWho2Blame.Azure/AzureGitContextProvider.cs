using GitWho2Blame.Azure.Options;
using GitWho2Blame.Cache.Abstractions;
using GitWho2Blame.Cache.Helpers;
using GitWho2Blame.MCP.Abstractions;
using GitWho2Blame.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace GitWho2Blame.Azure;

public class AzureGitContextProvider : IGitContextProvider
{
    private const string Azure = "azure";
    
    private readonly ILogger<AzureGitContextProvider> _logger;
    private readonly ICacheService _cache;
    private readonly AzureGitOptions _options;
    private readonly GitHttpClient _client;
    
    public AzureGitContextProvider(
        ILogger<AzureGitContextProvider> logger,
        IOptions<AzureGitOptions> options,
        IVssConnection connection,
        ICacheService cache)
    {
        _logger = logger;
        _cache = cache;
        _options = options.Value;
        _client = connection.GetClient<GitHttpClient>();
    }
    
    public async Task<List<CodeChangeSummary>> GetCodeChangesAsync(
        string relativeFilePath,
        string repoName,
        string owner,
        string currentBranchName,
        int startLine,
        int endLine,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        var repositories = await _cache.GetOrAddAsync(
            CacheKeyGenerator.GenerateKey(Azure, CacheKeyParts.Repositories, _options.ProjectId),
            async () => await _client.GetRepositoriesAsync(_options.ProjectId, cancellationToken: cancellationToken),
            CacheDurations.Long);

        if (repositories == null || repositories.Count == 0)
        {
            _logger.LogWarning("No repositories found in project {ProjectId}", _options.ProjectId);
            return [];
        }
        
        var repository = repositories.FirstOrDefault(r => r.Name.Equals(repoName, StringComparison.OrdinalIgnoreCase));
        if (repository == null)
        {
            throw new ArgumentException($"Repository '{repoName}' not found in project '{_options.ProjectId}'.");
        }
        
        _logger.LogInformation("Found repository {RepositoryName} with ID {RepositoryId} in project {ProjectId}",
            repository.Name, repository.Id, _options.ProjectId);
        
        var getCommits =  _client.GetCommitsAsync(
            _options.ProjectId,
            repository.Id,
            new GitQueryCommitsCriteria
            {
                ItemVersion = new GitVersionDescriptor
                {
                    VersionType = GitVersionType.Branch,
                    Version = currentBranchName,
                },
                ItemPath = relativeFilePath,
                FromDate = since.ToShortDateString(),
                HistoryMode = GitHistoryMode.FirstParent,
            },
            cancellationToken: cancellationToken);
        
        var commitSummaries = await _cache.GetOrAddAsync(
            CacheKeyGenerator.GenerateKey(Azure, CacheKeyParts.Commits, _options.ProjectId, repository.Id, relativeFilePath, since),
            async () => await getCommits,
            CacheDurations.Short);
        
        if (commitSummaries == null || commitSummaries.Count == 0)
        {
            _logger.LogInformation("No commits found for file {RelativeFilePath} in repository {RepoName} since {Since}",
                relativeFilePath, repoName, since);
            return [];
        }
        
        _logger.LogInformation("Found {CommitCount} commits for file {RelativeFilePath} in repository {RepoName} since {Since}",
            commitSummaries.Count, relativeFilePath, repoName, since);

        var changeSummaries = new List<CodeChangeSummary>();
        foreach (var commitSummary in commitSummaries)
        {
            var change = commitSummary.Changes.First();
            if (change is null)
            {
                _logger.LogWarning("No changes found for commit {CommitId}, skipping", commitSummary.CommitId);
                continue;
            }
            
            var commit = await _cache.GetOrAddAsync(
                CacheKeyGenerator.GenerateKey(Azure, CacheKeyParts.Commit, _options.ProjectId, repository.Id, commitSummary.CommitId),
                async () => await _client.GetCommitAsync(
                    _options.ProjectId,
                    commitSummary.CommitId,
                    repository.Id,
                    cancellationToken: cancellationToken),
                CacheDurations.Long);
            
            if (commit == null)
            {
                _logger.LogWarning("Commit {CommitId} not found in repository {RepoName}, skipping",
                    commitSummary.CommitId, repoName);
                continue;
            }
            
            var commitFileContentBytes = await _cache.GetOrAddAsync(
                CacheKeyGenerator.GenerateKey(Azure, CacheKeyParts.FileContent, _options.ProjectId, repository.Id, commitSummary.CommitId, relativeFilePath),
                async () => await GetItemContentBytesAsync(_options.ProjectId, repository.Id, relativeFilePath, commitSummary.CommitId, cancellationToken),
                CacheDurations.Long);
            
            if (commitFileContentBytes == null || commitFileContentBytes.Length == 0)
            {
                _logger.LogWarning("No content found for file {RelativeFilePath} in commit {CommitId}, skipping",
                    relativeFilePath, commitSummary.CommitId);
                continue;
            }

            CodeLine[] changedLines;
            var parentCommitId = commit.Parents.FirstOrDefault();
            if (parentCommitId == null || change.ChangeType == VersionControlChangeType.Add)
            {
                _logger.LogInformation("{RelativeFilePath} was added in Commit {CommitId}",
                    relativeFilePath,
                    commitSummary.CommitId);

                changedLines = ParseContent(
                    commitFileContentBytes, 
                    startLine, 
                    endLine,
                    CodeLine.Add);
            }
            else
            {
                _logger.LogInformation("{RelativeFilePath} was modified in Commit {CommitId}, comparing with parent commit {ParentCommitId}",
                    relativeFilePath,
                    commitSummary.CommitId,
                    parentCommitId);
                
                var parentCommitFileContentBytes = await _cache.GetOrAddAsync(
                CacheKeyGenerator.GenerateKey(Azure, CacheKeyParts.FileContent, _options.ProjectId, repository.Id, parentCommitId, relativeFilePath),
                async () => await GetItemContentBytesAsync(_options.ProjectId, repository.Id, relativeFilePath, parentCommitId, cancellationToken),
                CacheDurations.Long);

                if (parentCommitFileContentBytes == null || parentCommitFileContentBytes.Length == 0)
                {
                    _logger.LogWarning(
                        "No content found for file {RelativeFilePath} in parent commit {ParentCommitId}, skipping",
                        relativeFilePath, parentCommitId);
                    continue;
                }

                var getFileDiffs = _client.GetFileDiffsAsync(
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
                
                var diff = await _cache.GetOrAddAsync(
                    CacheKeyGenerator.GenerateKey(Azure, CacheKeyParts.FileDiffs, _options.ProjectId, repository.Id, parentCommitId, commitSummary.CommitId, relativeFilePath),
                    async () => await getFileDiffs,
                    CacheDurations.Long);
                
                if (diff == null || diff.Count == 0)
                {
                    _logger.LogWarning("No diffs found for file {RelativeFilePath} in commit {CommitId}, skipping",
                        relativeFilePath, commitSummary.CommitId);
                    continue;
                }

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
                
                changedLines = GetChangedLines(
                    parentCommitFileContentBytes,
                    commitFileContentBytes,
                    startLine,
                    endLine,
                    currentChangedLinesByType,
                    parentChangedLinesByType);
            }
            

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
    
    private async Task<byte[]> GetItemContentBytesAsync(
        Guid projectId,
        Guid repositoryId,
        string relativeFilePath,
        string commitId,
        CancellationToken cancellationToken)
    {
        await using var contentStream = await _client.GetItemContentAsync(
            projectId,
            repositoryId,
            relativeFilePath,
            versionDescriptor: new GitVersionDescriptor
            {
                Version = commitId,
                VersionType = GitVersionType.Commit
            },
            cancellationToken: cancellationToken);
        
        using var memoryStream = new MemoryStream();
        await contentStream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }
    
    private static CodeLine[] GetChangedLines(
        byte[] parentFileContent, 
        byte[] currentFileContent, 
        int startLine, 
        int endLine,
        Dictionary<LineDiffBlockChangeType, int[]> currentChangedLinesByType,
        Dictionary<LineDiffBlockChangeType, int[]> parentChangedLinesByType)
    {
        var parent = ParseContent(
            parentFileContent, 
            startLine, 
            endLine, 
            (changeType, lineNumber, line) =>
            {
                return changeType switch
                {
                    LineDiffBlockChangeType.Delete or LineDiffBlockChangeType.Edit => CodeLine.Delete(lineNumber, line),
                    _ => null
                };
            },
            parentChangedLinesByType);
        
        var current = ParseContent(
            currentFileContent, 
            startLine, 
            endLine, 
            (changeType, lineNumber, line) =>
            {
                return changeType switch
                {
                    LineDiffBlockChangeType.Add or LineDiffBlockChangeType.Edit => CodeLine.Add(lineNumber, line),
                    _ => null
                };
            },
            currentChangedLinesByType);
        
        var mergedLines = MergeChanges(parent, current);
        return mergedLines;
    }
    
    private static CodeLine[] ParseContent(
        byte[] fileContent,
        int startLine,
        int endLine,
        Func<LineDiffBlockChangeType, int, string, CodeLine?> lineHandler,
        Dictionary<LineDiffBlockChangeType, int[]>? changedLineNumbersByType = null)
    {
        var lines = new List<CodeLine>();
        using var memoryStream = new MemoryStream(fileContent);
        using var reader = new StreamReader(memoryStream);

        var currentLine = 1;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (currentLine >= startLine && currentLine <= endLine)
            {
                if (changedLineNumbersByType != null)
                {
                    var change = changedLineNumbersByType
                        .FirstOrDefault(kvp => kvp.Value.Contains(currentLine));
                    
                    var codeLine = lineHandler(change.Key, currentLine, line ?? string.Empty);
                    if (codeLine != null)
                    {
                        lines.Add(codeLine);
                    }
                }
                else
                {
                    // If no diff info, process every line in range.
                    // Pass a default change type, as it's an addition scenario.
                    var codeLine = lineHandler(LineDiffBlockChangeType.Add, currentLine, line ?? string.Empty);
                    if (codeLine != null)
                    {
                        lines.Add(codeLine);
                    }
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
    
    private static CodeLine[] ParseContent(
        byte[] fileContent,
        int startLine,
        int endLine,
        Func<int, string, CodeLine?> lineHandler)
    {
        return ParseContent(fileContent, startLine, endLine, (_, lineNumber, line) => lineHandler(lineNumber, line));
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