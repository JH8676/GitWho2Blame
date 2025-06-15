using GitWho2Blame.Models;

namespace GitWho2Blame.MCP.Abstractions;

public interface IGitService
{
    List<CodeLineChange> GetBlameForLinesAsync(string filePath, int startLine, int endLine);
}