namespace GitWho2Blame.Models;

public record CodeChangeSummary(
    string CommitSha,
    string Author,
    string Message,
    DateTimeOffset Date,
    CodeLine[] ChangedLines
);