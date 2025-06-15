using System.ComponentModel;
using System.Text.Json;
using GitWho2Blame.MCP.Abstractions;
using ModelContextProtocol.Server;

namespace GitWho2Blame.MCP;

[McpServerToolType]
public class Tools(IGitContextProvider gitContextProvider, IGitService gitService)
{
    [McpServerTool, Description("Gets the blame for a range of lines in a file.")]
    public string Blame(string path, int startLine, int endLine)
    {
        var changes = gitService.GetBlameForLinesAsync(path, startLine, endLine);
        return JsonSerializer.Serialize(changes);
    }
}
