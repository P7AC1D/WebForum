using FluentAssertions;
using System.Net;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;
using WebForum.IntegrationTests.Utilities;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;

namespace WebForum.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for AuthController API endpoints
/// Focuses on authentication, registration, token management, and security scenarios
/// </summary>
public class AuthControllerTests : IntegrationTestBase
{
  public AuthControllerTests(WebForumTestFactory factory) : base(factory)
  {
  }

  [Fact]
  public async Task Register_WithValidData_ShouldCreateUser()
  {
    // Arrange
    await InitializeTestAsync();

    var registrationRequest = new RegistrationRequest
    {
      Username = "validuser123",
      Email = "valid@example.com",
      Password = "SecurePassword123!",
      Role = UserRoles.User
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/auth/register", registrationRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var authResponse = await HttpUtilities.ReadAsAsync<AuthResponse>(response);
    authResponse.Should().NotBeNull();
    authResponse.AccessToken.Should().NotBeNullOrEmpty();
    authResponse.RefreshToken.Should().NotBeNullOrEmpty();
    authResponse.User.Should().NotBeNull();
    authResponse.User.Username.Should().Be(registrationRequest.Username);
    authResponse.User.Email.Should().Be(registrationRequest.Email);
    authResponse.User.Id.Should().BeGreaterThan(0);

    // Verify user was actually created in database
    var dbContext = GetDbContext();
    var createdUser = await dbContext.Users.FindAsync(authResponse.User.Id);
    createdUser.Should().NotBeNull();
    createdUser!.Username.Should().Be(registrationRequest.Username);
    createdUser.Email.Should().Be(registrationRequest.Email);
  }

  [Fact]
  public async Task Register_WithDuplicateEmail_ShouldReturnConflict()
  {
    // Arrange
    await InitializeTestAsync();
    var existingUser = await CreateTestUserAsync("existing", "existing@example.com");

    var duplicateRegistrationRequest = new RegistrationRequest
    {
      Username = "newuser",
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
  public async Task Register_WithDuplicateUsername_ShouldReturnConflict()
  {
    // Arrange
    await InitializeTestAsync();
    var existingUser = await CreateTestUserAsync("existinguser", "existing@example.com");

    var duplicateRegistrationRequest = new RegistrationRequest
    {
      Username = existingUser.Username, // Same username as existing user
      Email = "newemail@example.com",
      Password = "Password123!",
      Role = UserRoles.User
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/auth/register", duplicateRegistrationRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
  }

  [Theory]
  [InlineData("", "valid@example.com", "Password123!", "Username is required")]
  [InlineData("ab", "valid@example.com", "Password123!", "Username too short")]
  [InlineData("validuser", "", "Password123!", "Email is required")]
  [InlineData("validuser", "invalid-email", "Password123!", "Invalid email format")]
  [InlineData("validuser", "valid@example.com", "", "Password is required")]
  [InlineData("validuser", "valid@example.com", "123", "Password too short")]
  public async Task Register_WithInvalidData_ShouldReturnBadRequest(
      string username, string email, string password, string reason)
  {
    // Arrange
    await InitializeTestAsync();

    var invalidRegistrationRequest = new RegistrationRequest
    {
      Username = username,
      Email = email,
      Password = password,
      Role = UserRoles.User
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/auth/register", invalidRegistrationRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest, reason);
  }

  [Fact]
  public async Task Register_WithLongUsername_ShouldReturnBadRequest()
  {
    // Arrange
    await InitializeTestAsync();

    var registrationRequest = new RegistrationRequest
    {
      Username = new string('u', 51), // Over 50 character limit
      Email = "valid@example.com",
      Password = "Password123!",
      Role = UserRoles.User
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/auth/register", registrationRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Login_WithValidCredentials_ShouldReturnToken()
  {
    // Arrange
    await InitializeTestAsync();

    // First register a user
    var registrationRequest = new RegistrationRequest
    {
      Username = "loginuser",
      Email = "login@example.com",
      Password = "LoginPassword123!",
      Role = UserRoles.User
    };

    var registerResponse = await HttpUtilities.PostAsync(Client, "/api/auth/register", registrationRequest);
    registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    var registrationAuth = await HttpUtilities.ReadAsAsync<AuthResponse>(registerResponse);

    // Now test login
    var loginRequest = new LoginRequest
    {
      Email = registrationRequest.Email,
      Password = registrationRequest.Password
    };

    // Act
    var loginResponse = await HttpUtilities.PostAsync(Client, "/api/auth/login", loginRequest);

    // Assert
    loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var authResponse = await HttpUtilities.ReadAsAsync<AuthResponse>(loginResponse);
    authResponse.Should().NotBeNull();
    authResponse.AccessToken.Should().NotBeNullOrEmpty();
    authResponse.RefreshToken.Should().NotBeNullOrEmpty();
    authResponse.User.Should().NotBeNull();
    authResponse.User.Id.Should().Be(registrationAuth.User.Id);
    authResponse.User.Username.Should().Be(registrationRequest.Username);
    authResponse.User.Email.Should().Be(registrationRequest.Email);
  }

  [Fact]
  public async Task Login_WithInvalidEmail_ShouldReturnUnauthorized()
  {
    // Arrange
    await InitializeTestAsync();

    var loginRequest = new LoginRequest
    {
      Email = "nonexistent@example.com",
      Password = "SomePassword123!"
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/auth/login", loginRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
  {
    // Arrange
    await InitializeTestAsync();
    var existingUser = await CreateTestUserAsync("testuser", "test@example.com");

    var loginRequest = new LoginRequest
    {
      Email = existingUser.Email,
      Password = "WrongPassword123!"
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/auth/login", loginRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Theory]
  [InlineData("", "Password123!")]
  [InlineData("invalid-email", "Password123!")]
  [InlineData("valid@example.com", "")]
  public async Task Login_WithInvalidData_ShouldReturnBadRequest(string email, string password)
  {
    // Arrange
    await InitializeTestAsync();

    var loginRequest = new LoginRequest
    {
      Email = email,
      Password = password
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/auth/login", loginRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Refresh_WithValidToken_ShouldReturnNewToken()
  {
    // Arrange
    await InitializeTestAsync();

    // Register and login to get initial tokens
    var registrationRequest = new RegistrationRequest
    {
      Username = "refreshuser",
      Email = "refresh@example.com",
      Password = "RefreshPassword123!",
      Role = UserRoles.User
    };

    var registerResponse = await HttpUtilities.PostAsync(Client, "/api/auth/register", registrationRequest);
    var initialAuth = await HttpUtilities.ReadAsAsync<AuthResponse>(registerResponse);

    var refreshRequest = new RefreshToken
    {
      AccessToken = initialAuth.AccessToken,
      RefreshTokenValue = initialAuth.RefreshToken
    };

    // Act
    var refreshResponse = await HttpUtilities.PostAsync(Client, "/api/auth/refresh", refreshRequest);

    // Assert
    refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var newAuthResponse = await HttpUtilities.ReadAsAsync<AuthResponse>(refreshResponse);
    newAuthResponse.Should().NotBeNull();
    newAuthResponse.AccessToken.Should().NotBeNullOrEmpty();
    newAuthResponse.AccessToken.Should().NotBe(initialAuth.AccessToken); // Should be a new token
    newAuthResponse.RefreshToken.Should().NotBeNullOrEmpty();
    newAuthResponse.User.Id.Should().Be(initialAuth.User.Id);

    // Verify new token works for authenticated requests
    var authenticatedClient = Factory.CreateClient();
    authenticatedClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newAuthResponse.AccessToken);

    var testResponse = await authenticatedClient.GetAsync($"/api/users/{initialAuth.User.Id}");
    testResponse.StatusCode.Should().Be(HttpStatusCode.OK);
  }

  [Fact]
  public async Task Refresh_WithInvalidToken_ShouldReturnUnauthorized()
  {
    // Arrange
    await InitializeTestAsync();

    var refreshRequest = new RefreshToken
    {
      AccessToken = "invalid.access.token",
      RefreshTokenValue = "invalid.refresh.token"
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/auth/refresh", refreshRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Refresh_WithEmptyToken_ShouldReturnBadRequest()
  {
    // Arrange
    await InitializeTestAsync();

    var refreshRequest = new RefreshToken
    {
      AccessToken = "",
      RefreshTokenValue = ""
    };

    // Act
    var response = await HttpUtilities.PostAsync(Client, "/api/auth/refresh", refreshRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task AuthenticationFlow_RegisterLoginRefresh_ShouldMaintainUserIdentity()
  {
    // Arrange
    await InitializeTestAsync();

    var registrationRequest = new RegistrationRequest
    {
      Username = "flowuser",
      Email = "flow@example.com",
      Password = "FlowPassword123!",
      Role = UserRoles.User
    };

    // Act 1 - Register
    var registerResponse = await HttpUtilities.PostAsync(Client, "/api/auth/register", registrationRequest);
    registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    var registerAuth = await HttpUtilities.ReadAsAsync<AuthResponse>(registerResponse);

    // Act 2 - Login with same credentials
    var loginRequest = new LoginRequest
    {
      Email = registrationRequest.Email,
      Password = registrationRequest.Password
    };

    var loginResponse = await HttpUtilities.PostAsync(Client, "/api/auth/login", loginRequest);
    loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var loginAuth = await HttpUtilities.ReadAsAsync<AuthResponse>(loginResponse);

    // Act 3 - Refresh token
    var refreshRequest = new RefreshToken
    {
      AccessToken = loginAuth.AccessToken,
      RefreshTokenValue = loginAuth.RefreshToken
    };
    var refreshResponse = await HttpUtilities.PostAsync(Client, "/api/auth/refresh", refreshRequest);
    refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var refreshAuth = await HttpUtilities.ReadAsAsync<AuthResponse>(refreshResponse);

    // Assert - User identity should be consistent throughout
    var userIds = new[] { registerAuth.User.Id, loginAuth.User.Id, refreshAuth.User.Id };
    userIds.Should().AllBeEquivalentTo(registerAuth.User.Id);

    var usernames = new[] { registerAuth.User.Username, loginAuth.User.Username, refreshAuth.User.Username };
    usernames.Should().AllBeEquivalentTo(registrationRequest.Username);

    var emails = new[] { registerAuth.User.Email, loginAuth.User.Email, refreshAuth.User.Email };
    emails.Should().AllBeEquivalentTo(registrationRequest.Email);

    // All tokens should be different
    var tokens = new[] { registerAuth.AccessToken, loginAuth.AccessToken, refreshAuth.AccessToken };
    tokens.Should().OnlyHaveUniqueItems();
  }

  [Fact]
  public async Task Register_WithDifferentRoles_ShouldCreateUsersWithCorrectRoles()
  {
    // Arrange
    await InitializeTestAsync();

    var userRequest = new RegistrationRequest
    {
      Username = "regularuser",
      Email = "regular@example.com",
      Password = "Password123!",
      Role = UserRoles.User
    };

    var moderatorRequest = new RegistrationRequest
    {
      Username = "moderatoruser",
      Email = "moderator@example.com",
      Password = "Password123!",
      Role = UserRoles.Moderator
    };

    // Act
    var userResponse = await HttpUtilities.PostAsync(Client, "/api/auth/register", userRequest);
    var moderatorResponse = await HttpUtilities.PostAsync(Client, "/api/auth/register", moderatorRequest);

    // Assert
    userResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    moderatorResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    var userAuth = await HttpUtilities.ReadAsAsync<AuthResponse>(userResponse);
    var moderatorAuth = await HttpUtilities.ReadAsAsync<AuthResponse>(moderatorResponse);

    // Verify roles in database
    var dbContext = GetDbContext();
    var regularUser = await dbContext.Users.FindAsync(userAuth.User.Id);
    var moderatorUser = await dbContext.Users.FindAsync(moderatorAuth.User.Id);

    regularUser.Should().NotBeNull();
    regularUser!.Role.Should().Be(UserRoles.User);

    moderatorUser.Should().NotBeNull();
    moderatorUser!.Role.Should().Be(UserRoles.Moderator);
  }

  [Fact]
  public async Task ConcurrentRegistrations_ShouldHandleCorrectly()
  {
    // Arrange
    await InitializeTestAsync();

    var registrationTasks = Enumerable.Range(1, 10).Select(i => new RegistrationRequest
    {
      Username = $"concurrentuser{i}",
      Email = $"concurrent{i}@example.com",
      Password = "Password123!",
      Role = UserRoles.User
    }).Select(request => HttpUtilities.PostAsync(Client, "/api/auth/register", request));

    // Act
    var responses = await Task.WhenAll(registrationTasks);

    // Assert
    responses.Should().AllSatisfy(response =>
        response.StatusCode.Should().Be(HttpStatusCode.Created));

    var authResponses = await Task.WhenAll(responses.Select(r =>
        HttpUtilities.ReadAsAsync<AuthResponse>(r)));

    // All users should have unique IDs
    var userIds = authResponses.Select(auth => auth.User.Id).ToList();
    userIds.Should().OnlyHaveUniqueItems();

    // All tokens should be unique
    var tokens = authResponses.Select(auth => auth.AccessToken).ToList();
    tokens.Should().OnlyHaveUniqueItems();
  }
}
