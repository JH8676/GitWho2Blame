using GitWho2Blame.Models;

namespace GitWho2Blame.MCP.Abstractions;

public interface IGitService
{
    List<CodeLineChange> GetBlameForLinesAsync(string relativeFilePath, string repoRootPath, int startLine, int endLine);
    
    string? GetRepositoryOwner(string repoRootPath);
}