using GitWho2Blame.Models;

namespace GitWho2Blame.MCP.Abstractions;

public interface IGitContextProvider
{
    Task<List<CodeChangeSummary>> GetCodeChangesAsync(
        string relativeFilePath,
        string repoName,
        string owner,
        int startLine,
        int endLine,
        DateTime since,
        CancellationToken cancellationToken = default);
}