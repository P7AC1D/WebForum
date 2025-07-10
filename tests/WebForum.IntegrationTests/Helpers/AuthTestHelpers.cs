using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;

namespace WebForum.IntegrationTests.Helpers;

/// <summary>
/// Helper utilities for authentication-related testing operations
/// </summary>
public static class AuthTestHelpers
{
    /// <summary>
    /// Creates a test user with the specified role and returns user information
    /// </summary>
    public static async Task<UserInfo> CreateTestUserAsync(
        HttpClient client,
        string? username = null,
        string? email = null,
        UserRoles role = UserRoles.User,
        string? password = null)
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var actualUsername = username ?? $"user_{uniqueId}";
        var actualEmail = email ?? $"user_{uniqueId}@test.com";
        var actualPassword = password ?? "Test123!@#";

        var registrationRequest = new RegistrationRequest
        {
            Username = actualUsername,
            Email = actualEmail,
            Password = actualPassword
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", registrationRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return new UserInfo
        {
            Id = authResponse!.User.Id,
            Username = authResponse.User.Username,
            Email = authResponse.User.Email,
            Role = role.ToString()
        };
    }

    /// <summary>
    /// Creates multiple test users with unique credentials
    /// </summary>
    public static async Task<List<UserInfo>> CreateMultipleTestUsersAsync(
        HttpClient client,
        int count,
        UserRoles role = UserRoles.User)
    {
        var users = new List<UserInfo>();
        
        for (int i = 0; i < count; i++)
        {
            var user = await CreateTestUserAsync(
                client,
                username: $"user_{i}_{Guid.NewGuid():N}"[..16],
                email: $"user_{i}_{Guid.NewGuid():N}@test.com",
                role: role
            );
            users.Add(user);
        }
        
        return users;
    }

    /// <summary>
    /// Authenticates a user and returns the authentication response
    /// </summary>
    public static async Task<AuthResponse> LoginUserAsync(
        HttpClient client,
        string username,
        string password)
    {
        var loginRequest = new LoginRequest
        {
            Email = username,
            Password = password
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
        
        return authResponse!;
    }

    /// <summary>
    /// Creates a test user and immediately logs them in, returning both user info and auth response
    /// </summary>
    public static async Task<(UserInfo User, AuthResponse Auth)> CreateAndLoginTestUserAsync(
        HttpClient client,
        string? username = null,
        string? email = null,
        UserRoles role = UserRoles.User,
        string? password = null)
    {
        var actualPassword = password ?? "Test123!@#";
        var user = await CreateTestUserAsync(client, username, email, role, actualPassword);
        var auth = await LoginUserAsync(client, user.Username, actualPassword);
        
        return (user, auth);
    }

    /// <summary>
    /// Refreshes an authentication token
    /// </summary>
    public static async Task<AuthResponse> RefreshTokenAsync(
        HttpClient client,
        string accessToken,
        string? refreshToken = null)
    {
        var refreshRequest = new RefreshToken
        {
            AccessToken = accessToken,
            RefreshTokenValue = refreshToken
        };

        var response = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
        
        return authResponse!;
    }

    /// <summary>
    /// Validates that an authentication response contains expected data
    /// </summary>
    public static void ValidateAuthResponse(AuthResponse authResponse, string expectedUsername, string expectedEmail)
    {
        authResponse.Should().NotBeNull();
        authResponse.AccessToken.Should().NotBeEmpty();
        authResponse.RefreshToken.Should().NotBeEmpty();
        authResponse.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        
        authResponse.User.Should().NotBeNull();
        authResponse.User.Username.Should().Be(expectedUsername);
        authResponse.User.Email.Should().Be(expectedEmail);
        authResponse.User.Id.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Creates a valid registration request with random credentials
    /// </summary>
    public static RegistrationRequest CreateValidRegistrationRequest(
        string? username = null,
        string? email = null,
        string? password = null)
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var actualPassword = password ?? "Test123!@#";
        
        return new RegistrationRequest
        {
            Username = username ?? $"user_{uniqueId}",
            Email = email ?? $"user_{uniqueId}@test.com",
            Password = actualPassword
        };
    }

    /// <summary>
    /// Creates a valid login request
    /// </summary>
    public static LoginRequest CreateValidLoginRequest(string username, string password)
    {
        return new LoginRequest
        {
            Email = username,
            Password = password
        };
    }
}
