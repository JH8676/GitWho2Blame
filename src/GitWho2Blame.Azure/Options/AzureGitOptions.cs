namespace GitWho2Blame.Azure.Options;

public class AzureGitOptions
{
    public const string AzureGit = "AzureGit";
    
    public required string Token { get; set; }

    public required Uri OrgUri { get; set; }

    public Guid ProjectId { get; set; }
}