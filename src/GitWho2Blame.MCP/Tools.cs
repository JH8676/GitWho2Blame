using System.ComponentModel;
using System.Text.Json;
using GitWho2Blame.MCP.Abstractions;
using GitWho2Blame.Models.Requests;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace GitWho2Blame.MCP;

[McpServerToolType]
public class Tools(IGitService gitService, IGitContextProvider gitContextProvider, ILogger<Tools> logger)
{
    [McpServerTool, Description("Gets the blame for a range of lines in a file.")]
    public string Blame(BaseRequest request, int startLine, int endLine)
    {
        var changes = gitService.GetBlameForLinesAsync(
            request.RelativeFilePath, 
            request.RepoRootPath,
            startLine, 
            endLine);
        
        return JsonSerializer.Serialize(changes);
    }
    
    [McpServerTool, Description("Summarizes the history of changes in a specific section of a file via looking a diffs in git commits. The summary includes commit SHA, author, message, date, and the changed lines in the given line range.")]
    public async Task<string> GetCodeChangesSummaryAsync(BaseRequest request, string repoName, int startLine, int endLine)
    {
        logger.LogInformation("Getting code changes summary for {RelativeFilePath} in repository {RepoRootPath} from line {StartLine} to line {EndLine}",
            request.RelativeFilePath, 
            repoName, 
            startLine, 
            endLine);
        
        var owner = gitService.GetRepositoryOwner(request.RepoRootPath);
        if (owner == null)
        {
            logger.LogInformation("No owner found for {RepoRootPath}", request.RepoRootPath);
            return "No owner found for the repository.";
        }
        
        var changes = await gitContextProvider.GetCodeChangesAsync(
            request.RelativeFilePath,
            repoName,
            owner,
            startLine,
            endLine,
            DateTime.Now.AddDays(-60)); // TODO remove hardcoded date, use a parameter instead
        
        var response = JsonSerializer.Serialize(changes);
        return response;
    }
}
