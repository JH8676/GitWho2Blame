namespace GitWho2Blame.Models;

public record CodeLineChange
{
    public int Line { get; init; }
    
    public string Author { get; init; }
    
    public string Email { get; init; }
    
    public string CommitSha { get; init; }
    
    public DateTimeOffset CommitDate { get; init; }
}