namespace FrogBets.Api.Services;

public record AuthResult(string Token, DateTime ExpiresAt);

public interface IAuthService
{
    /// <summary>
    /// Validates credentials and returns a JWT token. Throws UnauthorizedAccessException on invalid credentials.
    /// </summary>
    Task<AuthResult> LoginAsync(string username, string password);

    /// <summary>
    /// Invalidates the given JWT token by adding its JTI to the blocklist.
    /// </summary>
    Task LogoutAsync(string token);

    /// <summary>
    /// Returns true if the given JTI has been blocklisted (logged out).
    /// </summary>
    bool IsTokenRevoked(string jti);

    /// <summary>
    /// Registers a new user with the given credentials and invite ID.
    /// Throws InvalidOperationException with codes: USERNAME_TAKEN, PASSWORD_TOO_SHORT.
    /// </summary>
    Task<AuthResult> RegisterAsync(string username, string password, Guid inviteId);
}
