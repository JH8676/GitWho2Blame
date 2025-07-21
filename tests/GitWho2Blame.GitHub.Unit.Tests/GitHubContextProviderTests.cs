using GitWho2Blame.Cache.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Octokit;

namespace GitWho2Blame.GitHub.Unit.Tests;

public class GitHubContextProviderTests
{
   public static TheoryData<string, (int line, string content)[], string> PatchTestCases => new()
    {
        {
            // Only additions
            """
            @@ -0,0 +1,2 @@
            +line1
            +line2
            """,
            new[] { (1, "+line1"), (2, "+line2") },
            "Only additions"
        },
        {
            // Only deletions
            """
            @@ -1,2 +0,0 @@
            -line1
            -line2
            """,
            new[] { (1, "-line1"), (2, "-line2") },
            "Only deletions"
        },
        {
            // Multiple hunks
            """
            @@ -1,2 +1,3 @@
             line1
            -line2
            +line2_modified
            +line3
            @@ -5,2 +6,3 @@
             line5
            -line6
            +line6_modified
            +line7
            """,
            new[] { (2, "-line2"), (2, "+line2_modified"), (3, "+line3"), (6, "-line6"), (7, "+line6_modified"), (8, "+line7") },
            "Multiple hunks"
        },
        {
            // Context lines only (no changes)
            """
            @@ -1,2 +1,2 @@
             line1
             line2
            """,
            Array.Empty<(int, string)>(),
            "Context lines only"
        },
        {
            // End-of-file marker
            """
            @@ -1,2 +1,2 @@
             line1
            -line2
            +line2_modified
            \ No newline at end of file
            """,
            new[] { (2, "-line2"), (2, "+line2_modified") },
            "End-of-file marker"
        },
        {
            // Interleaved additions and deletions (modification in the middle)
            """
            @@ -3,7 +3,7 @@
             line3
             line4
            -line5
            +line5_changed
             line6
            -line7
            +line7_changed
             line8
             line9
            """,
            new[] { (5, "-line5"), (5, "+line5_changed"), (7, "-line7"), (7, "+line7_changed") },
            "Interleaved additions and deletions"
        },
        {
            // Zero-length hunk (no lines in hunk)
            """
            @@ -10,0 +11,0 @@
            """,
            Array.Empty<(int, string)>(),
            "Zero-length hunk"
        },
        {
            // Rename with patch
            """
            @@ -1,2 +1,2 @@
            -oldline
            +newline
            """,
            new[] { (1, "-oldline"), (1, "+newline") },
            "Rename with patch"
        }
    };

    [Theory]
    [MemberData(nameof(PatchTestCases))]
    public async Task GetCodeChangesAsync_ParsesPatchEdgeCases(
        string patch,
        (int line, string content)[] expectedLines, string description)
    {
        // Arrange
        var logger = Mock.Of<ILogger<GitHubContextProvider>>();
        var clientMock = new Mock<IGitHubClient>();
        var cacheMock = new Mock<ICacheService>();

        var file = new GitHubCommitFile(
            filename: "test.txt",
            additions: 1,
            deletions: 1,
            changes: 2,
            status: "modified",
            blobUrl: "",
            contentsUrl: "",
            rawUrl: "",
            sha: "fileSha",
            patch: patch,
            previousFileName: null
        );

        var author = new Committer(
            name: "Test",
            email: "test@example.com",
            date: DateTimeOffset.UtcNow
        );

        var commit = new Commit(
            nodeId: "",
            url: "",
            label: "",
            @ref: "",
            sha: "commitSha",
            user: null,
            repository: null,
            message: "Test commit",
            author: author,
            committer: author,
            tree: null,
            parents: new List<GitReference>(),
            commentCount: 0,
            verification: null
        );

        var githubCommit = new GitHubCommit(
            nodeId: "",
            url: "",
            label: "",
            @ref: "",
            sha: "abc123",
            user: null,
            repository: null,
            author: null,
            commentsUrl: "",
            commit: commit,
            committer: null,
            htmlUrl: "",
            stats: null,
            parents: new List<GitReference>(),
            files: new List<GitHubCommitFile> { file }
        );

        var commitSummary = new GitHubCommit(
            nodeId: "",
            url: "",
            label: "",
            @ref: "",
            sha: "abc123",
            user: null,
            repository: null,
            author: null,
            commentsUrl: "",
            commit: commit,
            committer: null,
            htmlUrl: "",
            stats: null,
            parents: new List<GitReference>(),
            files: new List<GitHubCommitFile>()
        );

        cacheMock.Setup(c => c.GetOrAddAsync(
                It.Is<string>(k => k.StartsWith("github:commits:")),
                It.IsAny<Func<Task<IReadOnlyList<GitHubCommit>?>>>(),
                It.IsAny<TimeSpan>()))
            .ReturnsAsync(new List<GitHubCommit> { commitSummary });
        
        cacheMock.Setup(c => c.GetOrAddAsync(
                It.Is<string>(k => k.StartsWith("github:commit:")),
                It.IsAny<Func<Task<GitHubCommit?>>>(),
                It.IsAny<TimeSpan>()))
            .ReturnsAsync(githubCommit);

        var provider = new GitHubContextProvider(logger, clientMock.Object, cacheMock.Object);

        // Act
        var result = await provider.GetCodeChangesAsync(
            "test.txt",  "root/path","repo", "owner",  "main", 1, 10, DateTime.UtcNow.AddDays(-1));

        // Assert
        var changedLines = result[0].ChangedLines;
        Assert.Equal(expectedLines.Length, changedLines.Length);

        foreach (var (line, content) in expectedLines)
        {
            Assert.Contains(changedLines, l => l.LineNumber == line && l.Content.Contains(content));
        }
    }
}