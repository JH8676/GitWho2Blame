using System.ComponentModel;
using GitWho2Blame.MCP.Abstractions;
using GitWho2Blame.Models;
using GitWho2Blame.Models.Requests;
using GitWho2Blame.Models.Responses;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace GitWho2Blame.MCP;

[McpServerToolType]
public class Tools(IGitService gitService, IGitContextProvider gitContextProvider, ILogger<Tools> logger)
{
    [McpServerTool, Description("Summarizes the history of changes in a specific section of a file via looking a diffs in git commits. The summary includes commit SHA, author, message, date, and the changed lines in the given line range.")]
    public async Task<Response<List<CodeChangeSummary>>> GetCodeChangesSummaryAsync(
        BaseRequest request, 
        string repoName,
        int startLine, 
        int endLine,
        [Description("Only include changes since this date. Format: ISO 8601 (e.g., 2024-06-20T15:30:00Z).")] DateTime since)
    {
        logger.LogInformation("Getting code changes summary for {RelativeFilePath} in repository {RepoRootPath} from line {StartLine} to line {EndLine}",
            request.RelativeFilePath, 
            repoName, 
            startLine, 
            endLine);
        
        var currentBranchName = gitService.GetCurrentBranchName(request.RepoRootPath);
        if (string.IsNullOrEmpty(currentBranchName))
        {
            logger.LogWarning("Could not determine the current branch for {RepoRootPath}", request.RepoRootPath);
            return Response<List<CodeChangeSummary>>.Failure("Could not determine the current branch.");
        }
        
        var owner = gitService.GetRepositoryOwner(request.RepoRootPath);
        if (owner == null)
        {
            logger.LogInformation("No owner found for {RepoRootPath}", request.RepoRootPath);
            return Response<List<CodeChangeSummary>>.Failure("Repository owner not found.");
        }
        
        var changes = await gitContextProvider.GetCodeChangesAsync(
            request.RelativeFilePath,
            request.RepoRootPath,
            repoName,
            owner,
            currentBranchName,
            startLine,
            endLine,
            since);
        
        return Response<List<CodeChangeSummary>>.Success(changes);
    }
}
