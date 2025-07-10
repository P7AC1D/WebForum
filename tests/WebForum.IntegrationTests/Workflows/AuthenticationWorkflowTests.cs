using FluentAssertions;
using System.Net;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;
using WebForum.IntegrationTests.Utilities;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;

namespace WebForum.IntegrationTests.Workflows;

/// <summary>
/// Integration tests for complete authentication and authorization workflows
/// Covers registration → login → token refresh → role-based access scenarios
/// </summary>
public class AuthenticationWorkflowTests : IntegrationTestBase
{
  public AuthenticationWorkflowTests(WebForumTestFactory factory) : base(factory)
  {
  }

  [Fact]
  public async Task CompleteAuthenticationFlow_RegisterLoginCreateContent_ShouldWork()
  {
    // Arrange
        var registrationRequest = new RegistrationRequest
    {
      Username = "newuser123",
      Email = "newuser@example.com",
      Password = "SecurePassword123!",
      Role = UserRoles.User
    };

    // Act & Assert - Registration
    var registerResponse = await HttpUtilities.PostAsync(Client, "/api/auth/register", registrationRequest);
    registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    var authResponse = await HttpUtilities.ReadAsAsync<AuthResponse>(registerResponse);
    authResponse.Should().NotBeNull();
    authResponse.AccessToken.Should().NotBeNullOrEmpty();
    authResponse.RefreshToken.Should().NotBeNullOrEmpty();
    authResponse.User.Username.Should().Be(registrationRequest.Username);
    authResponse.User.Email.Should().Be(registrationRequest.Email);

    // Act & Assert - Use token to create content
    var authenticatedClient = Factory.CreateClient();
    authenticatedClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.AccessToken);

    var createPostRequest = new CreatePostRequest
    {
      Title = "My First Post After Registration",
      Content = "This is my first post content after registering and logging in."
    };

    var createPostResponse = await HttpUtilities.PostAsync(authenticatedClient, "/api/posts", createPostRequest);
    createPostResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    var createdPost = await HttpUtilities.ReadAsAsync<PostResponse>(createPostResponse);
    createdPost.Should().NotBeNull();
    createdPost.Title.Should().Be(createPostRequest.Title);
    createdPost.AuthorId.Should().Be(authResponse.User.Id);
  }

  [Fact]
  public async Task LoginRefreshTokenFlow_ShouldMaintainAuthentication()
  {
    // Arrange
    var testUser = await CreateTestUserAsync("loginuser", "login@example.com");

    var loginRequest = new LoginRequest
    {
      Email = testUser.Email,
      Password = "TestPassword123!" // Use the correct password that matches BCrypt hash
    };

    // Act & Assert - Login
    var loginResponse = await HttpUtilities.PostAsync(Client, "/api/auth/login", loginRequest);
    loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var loginAuthResponse = await HttpUtilities.ReadAsAsync<AuthResponse>(loginResponse);
    loginAuthResponse.Should().NotBeNull();
    loginAuthResponse.AccessToken.Should().NotBeNullOrEmpty();

    var originalToken = loginAuthResponse.AccessToken;
    var refreshToken = loginAuthResponse.RefreshToken;

    // Act & Assert - Refresh Token
    var refreshRequest = new RefreshToken
    {
      AccessToken = originalToken,
      RefreshTokenValue = refreshToken
    };
    var refreshResponse = await HttpUtilities.PostAsync(Client, "/api/auth/refresh", refreshRequest);
    refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var refreshAuthResponse = await HttpUtilities.ReadAsAsync<AuthResponse>(refreshResponse);
    refreshAuthResponse.Should().NotBeNull();
    refreshAuthResponse.AccessToken.Should().NotBeNullOrEmpty();
    refreshAuthResponse.AccessToken.Should().NotBe(originalToken); // New token should be different

    // Act & Assert - Use new token
    var authenticatedClient = Factory.CreateClient();
    authenticatedClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", refreshAuthResponse.AccessToken);

    var userResponse = await authenticatedClient.GetAsync($"/api/users/{testUser.Id}");
    userResponse.StatusCode.Should().Be(HttpStatusCode.OK);
  }

  [Fact]
  public async Task RoleBasedAccess_ShouldEnforcePermissions()
  {
    // Arrange
    var regularUser = await CreateTestUserAsync("regular", "regular@example.com", UserRoles.User);
    var moderator = await CreateTestUserAsync("moderator", "mod@example.com", UserRoles.Moderator);

    var regularUserClient = CreateAuthenticatedClient(regularUser.Id, regularUser.Username, UserRoles.User);
    var moderatorClient = CreateAuthenticatedClient(moderator.Id, moderator.Username, UserRoles.Moderator);

    // Create a post as regular user
    var createPostRequest = new CreatePostRequest
    {
      Title = "Test Post for Moderation",
      Content = "This post will be used to test moderation features."
    };

    var createResponse = await HttpUtilities.PostAsync(regularUserClient, "/api/posts", createPostRequest);
    createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    var post = await HttpUtilities.ReadAsAsync<PostResponse>(createResponse);

    // Act & Assert - Regular user should NOT be able to access moderator endpoints
    var moderatorEndpointResponse = await regularUserClient.GetAsync("/api/posts/tagged");
    moderatorEndpointResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);

    // Act & Assert - Moderator SHOULD be able to access moderator endpoints
    var moderatorAccessResponse = await moderatorClient.GetAsync("/api/posts/tagged");
    moderatorAccessResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task InvalidCredentials_ShouldReturnUnauthorized()
  {
    // Arrange
    var invalidLoginRequest = new LoginRequest
    {
      Email = "nonexistent@example.com",
      Password = "wrongpassword"
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/auth/login", invalidLoginRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task ExpiredToken_ShouldRequireRefresh()
  {
    // Arrange
    var testUser = await CreateTestUserAsync();

    // Create a JWT token that's expired (valid format but expired)
    var expiredToken = TestAuthenticationHelper.GenerateJwtToken(
        testUser.Id,
        testUser.Username,
        testUser.Email,
        testUser.Role,
        expirationMinutes: -10); // Already expired 10 minutes ago

    var clientWithExpiredToken = Factory.CreateClient();
    clientWithExpiredToken.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

    // Act - Try to access a protected endpoint (CreatePost requires authentication)
    var createPostRequest = new CreatePostRequest
    {
      Title = "Test Post",
      Content = "This should fail with expired token"
    };

    var response = await HttpUtilities.PostAsync(clientWithExpiredToken, "/api/posts", createPostRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task DuplicateRegistration_ShouldReturnConflict()
  {
    // Arrange
    var existingUser = await CreateTestUserAsync("existing", "existing@example.com");

    var duplicateRegistrationRequest = new RegistrationRequest
    {
      Username = "different",
      Email = existingUser.Email, // Same email as existing user
      Password = "Password123!",
      Role = UserRoles.User
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/auth/register", duplicateRegistrationRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task UnauthorizedAccess_ToProtectedEndpoint_ShouldReturnUnauthorized()
  {
    // Arrange
    var createPostRequest = new CreatePostRequest
    {
      Title = "Unauthorized Post",
      Content = "This should not be created without authentication."
    };

    // Act - Attempt to create post without authentication
    var response = await HttpUtilities.PostAsync(Client, "/api/posts", createPostRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }
}
