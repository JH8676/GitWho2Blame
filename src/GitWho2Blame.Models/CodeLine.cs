namespace GitWho2Blame.Models;

public record CodeLine
{
    public int LineNumber { get; init; }
    
    public required string Content { get; init; }

    public static CodeLine Add(int lineNumber, string content)
        => new()
        {
            LineNumber = lineNumber,
            Content = $"+{content}"
        };
    
    public static CodeLine Delete(int lineNumber, string content)
        => new()
        {
            LineNumber = lineNumber,
            Content = $"-{content}"
        };
}