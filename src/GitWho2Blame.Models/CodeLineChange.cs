namespace GitWho2Blame.Models;

public record CodeLineChange
{
    public int LineNumber { get; init; }
    
    public required string Author { get; init; }
    
    public required string Email { get; init; }
    
    public required string CommitSha { get; init; }
    
    public DateTimeOffset CommitDate { get; init; }
}