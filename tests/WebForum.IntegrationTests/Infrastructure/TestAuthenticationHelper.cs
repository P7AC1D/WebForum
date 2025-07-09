using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WebForum.Api.Models;

namespace WebForum.IntegrationTests.Infrastructure;

/// <summary>
/// Utilities for generating JWT tokens for testing authentication
/// </summary>
public static class TestAuthenticationHelper
{
    private const string TestSecretKey = "TestSecretKeyThatIsAtLeast32CharactersLongForTesting!";
    private const string TestIssuer = "WebForumTestApi";
    private const string TestAudience = "WebForumTestUsers";

    /// <summary>
    /// Generates a JWT token for testing with specified user information
    /// </summary>
    /// <param name="userId">User ID for the token</param>
    /// <param name="username">Username for the token</param>
    /// <param name="roles">User roles for authorization</param>
    /// <param name="expirationMinutes">Token expiration time in minutes (default: 60)</param>
    /// <returns>JWT token string</returns>
    public static string GenerateJwtToken(
        int userId, 
        string username, 
        UserRoles roles = UserRoles.User, 
        int expirationMinutes = 60)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(TestSecretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new("username", username)
        };

        // Add role claims
        if (roles.HasFlag(UserRoles.User))
            claims.Add(new Claim(ClaimTypes.Role, "User"));
        if (roles.HasFlag(UserRoles.Moderator))
            claims.Add(new Claim(ClaimTypes.Role, "Moderator"));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            Issuer = TestIssuer,
            Audience = TestAudience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Creates an HTTP client with authentication header set
    /// </summary>
    /// <param name="factory">Web application factory</param>
    /// <param name="userId">User ID for the token</param>
    /// <param name="username">Username for the token</param>
    /// <param name="roles">User roles for authorization</param>
    /// <returns>Authenticated HTTP client</returns>
    public static HttpClient CreateAuthenticatedClient(
        WebForumTestFactory factory,
        int userId,
        string username,
        UserRoles roles = UserRoles.User)
    {
        var client = factory.CreateClient();
        var token = GenerateJwtToken(userId, username, roles);
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Gets authorization header value for use in HTTP requests
    /// </summary>
    /// <param name="userId">User ID for the token</param>
    /// <param name="username">Username for the token</param>
    /// <param name="roles">User roles for authorization</param>
    /// <returns>Authorization header value</returns>
    public static string GetAuthorizationHeaderValue(
        int userId,
        string username,
        UserRoles roles = UserRoles.User)
    {
        var token = GenerateJwtToken(userId, username, roles);
        return $"Bearer {token}";
    }

    /// <summary>
    /// Test user data for common scenarios
    /// </summary>
    public static class TestUsers
    {
        public static readonly (int Id, string Username, UserRoles Roles) RegularUser = 
            (1, "testuser", UserRoles.User);
        
        public static readonly (int Id, string Username, UserRoles Roles) Moderator = 
            (2, "testmod", UserRoles.User | UserRoles.Moderator);
        
        public static readonly (int Id, string Username, UserRoles Roles) SuperModerator = 
            (3, "testsupermod", UserRoles.Moderator);
    }
}
