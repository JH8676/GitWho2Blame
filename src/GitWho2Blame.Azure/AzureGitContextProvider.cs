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
        IOptions<AzureGitOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        var connection = new VssConnection(
            _options.OrgUri,
            new VssBasicCredential(string.Empty, _options.Token));
        _client = connection.GetClient<GitHttpClient>();
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
        // caching, this doesnt change much
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
        
        var commits = await _client.GetCommitsAsync(
            _options.ProjectId,
            repository.Id,
            new GitQueryCommitsCriteria
            {
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
            commits.Count, relativeFilePath, repoName, since);

        foreach (var commit in commits)
        {
            var relevantChanges = commit.Changes
                .Where(c => c.Item.Path.Equals(relativeFilePath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            _logger.LogInformation("Processing commit {CommitId} with {ChangeCount} changes for file {RelativeFilePath}",
                commit.CommitId, relevantChanges.Count, relativeFilePath);

            foreach (var change in relevantChanges)
            {
                _logger.LogInformation("Change type: {ChangeType}, New content: {NewContent}",
                    change.ChangeType, change.NewContent?.Content ?? "No content");
            }
        }
        
        return new List<CodeChangeSummary>();
    }
}