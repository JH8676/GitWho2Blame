namespace GitWho2Blame.Models.Requests;

public record BaseRequest(
    string RepoRootPath,
    string RelativeFilePath);