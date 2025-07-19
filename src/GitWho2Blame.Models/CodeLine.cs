namespace GitWho2Blame.Models;

public class CodeLine
{
    
    public int LineNumber { get; set; }
    
    public required string Content { get; set; }

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