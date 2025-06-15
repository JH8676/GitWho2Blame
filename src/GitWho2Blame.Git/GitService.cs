using GitWho2Blame.MCP.Abstractions;
using GitWho2Blame.Models;
using LibGit2Sharp;

namespace GitWho2Blame.Git;

public class GitService : IGitService
{
    private readonly string _repositoryPath;

    public GitService(string repositoryPath)
    {
        _repositoryPath = repositoryPath;
        // Initialize any required services or configurations here
    }

    public List<CodeLineChange> GetBlameForLinesAsync(string filePath, int startLine, int endLine)
    {
        var result = new List<CodeLineChange>();

        var repoPath = Repository.Discover(_repositoryPath);
        using var repo = new Repository(repoPath);
        var relPath = Path.GetRelativePath(repoPath, filePath);
        var blame = repo.Blame(relPath);
        
        foreach (var hunk in blame)
        {
            for (int i = 0; i < hunk.LineCount; i++)
            {
                int line = hunk.FinalStartLineNumber + i;
                if (line >= startLine && line <= endLine)
                {
                    result.Add(new CodeLineChange
                    {
                        Line = line,
                        Author = hunk.FinalSignature.Name,
                        Email = hunk.FinalSignature.Email,
                        CommitSha = hunk.FinalCommit.Sha,
                        CommitDate = hunk.FinalCommit.Committer.When
                    });
                }
            }
        }

        return result;
    }
}