namespace GLMS.Web.Models;

public sealed class EmailVerificationChallenge
{
    public string UserId { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public int FailedAttempts { get; set; }
}
