using GitWho2Blame.Models;

namespace GitWho2Blame.Common.Helpers;

public static class GitDiffParser
{
    private const string EndOfFileMarker = @"\ No newline at end of file";
    
    public static List<CodeLine> ParsePatch(string patchContent, int startLine, int endLine)
    {
        var linesChanged = new List<CodeLine>();
        var patchLines = patchContent.Split(Environment.NewLine);

        for (var i = 0; i < patchLines.Length; i++)
        {
            var line = patchLines[i];
            var hunkHeaderMatch = RegexHelpers.GitHubHunkHeaderRegex().Match(line);
            if (!hunkHeaderMatch.Success)
            {
                continue;
            }

            var originalStart = int.Parse(hunkHeaderMatch.Groups[1].Value);
            var newStart = int.Parse(hunkHeaderMatch.Groups[3].Value);
            var newCount = string.IsNullOrEmpty(hunkHeaderMatch.Groups[4].Value) ? 1 : int.Parse(hunkHeaderMatch.Groups[4].Value);
            var newEnd = newStart + newCount - 1;

            if (newEnd < startLine && newStart > endLine)
            {
                continue;
            }

            i++; // Move to the first line of the hunk content

            var addedLineCount = 0;
            var deletedLineCount = 0;
            var unchangedLineCount = 0;
            while (i < patchLines.Length)
            {
                var currentLine = patchLines[i];
                if (RegexHelpers.GitHubHunkHeaderRegex().Match(currentLine).Success)
                {
                    i--;
                    break;
                }
                
                if (currentLine.Length == 0)
                {
                    i++;
                    continue; // Skip empty lines
                }
                
                if (currentLine == EndOfFileMarker)
                {
                    i++;
                    continue; // Skip the end of file marker
                }
                
                switch (currentLine[0])
                {
                    case '+':
                        var newLineNumber = newStart + addedLineCount + unchangedLineCount;
                        AddLineIfInRange(linesChanged, newLineNumber, currentLine, startLine, endLine);
                        
                        addedLineCount++;
                        break;
                    case '-':
                        var originalLineNumber = originalStart + deletedLineCount + unchangedLineCount;
                        AddLineIfInRange(linesChanged, originalLineNumber, currentLine, startLine, endLine);
                            
                        deletedLineCount++;
                        break;
                
                    default:
                        unchangedLineCount++;
                        break;
                }
                
                i++;
            }
        }
        return linesChanged;
    }
    
    private static void AddLineIfInRange(
        List<CodeLine> linesChanged, 
        int lineNumber, 
        string content, 
        int startLine, 
        int endLine)
    {
        if (lineNumber >= startLine && lineNumber <= endLine)
        {
            linesChanged.Add(new CodeLine
            {
                LineNumber = lineNumber,
                Content = content
            });
        }
    }
}