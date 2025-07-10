using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WebForum.Api.Data;
using WebForum.Api.Data.DTOs;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;
using WebForum.Api.Services.Implementations;
using WebForum.Api.Services.Interfaces;
using WebForum.UnitTests.Helpers;
using Xunit;

namespace WebForum.UnitTests.Services;

public class AuthServiceTests : IDisposable
{
  private readonly ForumDbContext _context;
  private readonly Mock<ISecurityService> _mockSecurityService;
  private readonly Mock<IUserService> _mockUserService;
  private readonly Mock<ILogger<AuthService>> _mockLogger;
  private readonly AuthService _authService;

  public AuthServiceTests()
  {
    var options = new DbContextOptionsBuilder<ForumDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options;

    _context = new ForumDbContext(options);
    _mockSecurityService = new Mock<ISecurityService>();
    _mockUserService = new Mock<IUserService>();
    _mockLogger = new Mock<ILogger<AuthService>>();

    _authService = new AuthService(
        _context,
        _mockSecurityService.Object,
        _mockUserService.Object,
        _mockLogger.Object);
  }

  public void Dispose()
  {
    _context.Dispose();
  }

  #region RegisterAsync Tests

  [Fact]
  public async Task RegisterAsync_WithNullRegistration_ThrowsArgumentException()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _authService.RegisterAsync(null!));
  }

  [Fact]
  public async Task RegisterAsync_WithExistingEmail_ThrowsInvalidOperationException()
  {
    // Arrange
    var registration = TestHelper.CreateValidRegistrationRequest();
    var existingUser = TestHelper.CreateValidUser();

    _mockUserService.Setup(x => x.GetUserByEmailAsync(registration.Email))
        .ReturnsAsync(existingUser);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        _authService.RegisterAsync(registration));

    exception.Message.Should().Contain("email address already exists");
  }

  [Fact]
  public async Task RegisterAsync_WithExistingUsername_ThrowsInvalidOperationException()
  {
    // Arrange
    var registration = TestHelper.CreateValidRegistrationRequest();
    var existingUser = TestHelper.CreateValidUser();

    _mockUserService.Setup(x => x.GetUserByEmailAsync(registration.Email))
        .ReturnsAsync((User?)null);
    _mockUserService.Setup(x => x.GetUserByUsernameAsync(registration.Username))
        .ReturnsAsync(existingUser);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        _authService.RegisterAsync(registration));

    exception.Message.Should().Contain("username already exists");
  }

  [Fact]
  public async Task RegisterAsync_WithValidData_ReturnsAuthResponse()
  {
    // Arrange
    var registration = TestHelper.CreateValidRegistrationRequest();
    var hashedPassword = "hashedPassword123";
    var token = "jwt.token.here";
    var refreshToken = "refresh.token.here";
    var expiresIn = 3600;

    _mockUserService.Setup(x => x.GetUserByEmailAsync(registration.Email))
        .ReturnsAsync((User?)null);
    _mockUserService.Setup(x => x.GetUserByUsernameAsync(registration.Username))
        .ReturnsAsync((User?)null);
    _mockSecurityService.Setup(x => x.HashPassword(registration.Password))
        .Returns(hashedPassword);
    _mockSecurityService.Setup(x => x.GenerateJwtToken(It.IsAny<User>()))
        .Returns(token);
    _mockSecurityService.Setup(x => x.GenerateRefreshToken())
        .Returns(refreshToken);
    _mockSecurityService.Setup(x => x.GetTokenExpirationSeconds())
        .Returns(expiresIn);

    // No database setup needed for in-memory database

    // Act
    var result = await _authService.RegisterAsync(registration);

    // Assert
    result.Should().NotBeNull();
    result.AccessToken.Should().Be(token);
    result.RefreshToken.Should().Be(refreshToken);
    result.ExpiresIn.Should().Be(expiresIn);
    result.User.Should().NotBeNull();
    result.User.Email.Should().Be(registration.Email);
    result.User.Username.Should().Be(registration.Username);
  }

  #endregion

  #region LoginAsync Tests

  [Fact]
  public async Task LoginAsync_WithNullLogin_ThrowsArgumentException()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _authService.LoginAsync(null!));
  }

  [Fact]
  public async Task LoginAsync_WithEmailFormat_SearchesByEmail()
  {
    // Arrange
    var login = new LoginRequest { Email = "user@example.com", Password = "password123" };
    var user = TestHelper.CreateValidUser();

    _mockUserService.Setup(x => x.GetUserByEmailAsync(login.Email))
        .ReturnsAsync(user);
    _mockSecurityService.Setup(x => x.VerifyPassword(login.Password, user.PasswordHash))
        .Returns(true);
    _mockSecurityService.Setup(x => x.GenerateJwtToken(user))
        .Returns("token");
    _mockSecurityService.Setup(x => x.GenerateRefreshToken())
        .Returns("refresh");
    _mockSecurityService.Setup(x => x.GetTokenExpirationSeconds())
        .Returns(3600);

    // Act
    var result = await _authService.LoginAsync(login);

    // Assert
    result.Should().NotBeNull();
    _mockUserService.Verify(x => x.GetUserByEmailAsync(login.Email), Times.Once);
    _mockUserService.Verify(x => x.GetUserByUsernameAsync(It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public async Task LoginAsync_WithUsernameFormat_SearchesByUsername()
  {
    // Arrange
    var login = new LoginRequest { Email = "username", Password = "password123" };
    var user = TestHelper.CreateValidUser();

    _mockUserService.Setup(x => x.GetUserByUsernameAsync(login.Email))
        .ReturnsAsync(user);
    _mockSecurityService.Setup(x => x.VerifyPassword(login.Password, user.PasswordHash))
        .Returns(true);
    _mockSecurityService.Setup(x => x.GenerateJwtToken(user))
        .Returns("token");
    _mockSecurityService.Setup(x => x.GenerateRefreshToken())
        .Returns("refresh");
    _mockSecurityService.Setup(x => x.GetTokenExpirationSeconds())
        .Returns(3600);

    // Act
    var result = await _authService.LoginAsync(login);

    // Assert
    result.Should().NotBeNull();
    _mockUserService.Verify(x => x.GetUserByUsernameAsync(login.Email), Times.Once);
    _mockUserService.Verify(x => x.GetUserByEmailAsync(It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public async Task LoginAsync_WithNonExistentUser_ThrowsUnauthorizedAccessException()
  {
    // Arrange
    var login = new LoginRequest { Email = "nonexistent@example.com", Password = "password123" };

    _mockUserService.Setup(x => x.GetUserByEmailAsync(login.Email))
        .ReturnsAsync((User?)null);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        _authService.LoginAsync(login));

    exception.Message.Should().Contain("Invalid username/email or password");
  }

  [Fact]
  public async Task LoginAsync_WithInvalidPassword_ThrowsUnauthorizedAccessException()
  {
    // Arrange
    var login = new LoginRequest { Email = "user@example.com", Password = "wrongpassword" };
    var user = TestHelper.CreateValidUser();

    _mockUserService.Setup(x => x.GetUserByEmailAsync(login.Email))
        .ReturnsAsync(user);
    _mockSecurityService.Setup(x => x.VerifyPassword(login.Password, user.PasswordHash))
        .Returns(false);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        _authService.LoginAsync(login));

    exception.Message.Should().Contain("Invalid username/email or password");
  }

  [Fact]
  public async Task LoginAsync_WithValidCredentials_ReturnsAuthResponse()
  {
    // Arrange
    var login = new LoginRequest { Email = "user@example.com", Password = "password123" };
    var user = TestHelper.CreateValidUser();
    var token = "jwt.token.here";
    var refreshToken = "refresh.token.here";
    var expiresIn = 3600;

    _mockUserService.Setup(x => x.GetUserByEmailAsync(login.Email))
        .ReturnsAsync(user);
    _mockSecurityService.Setup(x => x.VerifyPassword(login.Password, user.PasswordHash))
        .Returns(true);
    _mockSecurityService.Setup(x => x.GenerateJwtToken(user))
        .Returns(token);
    _mockSecurityService.Setup(x => x.GenerateRefreshToken())
        .Returns(refreshToken);
    _mockSecurityService.Setup(x => x.GetTokenExpirationSeconds())
        .Returns(expiresIn);

    // Act
    var result = await _authService.LoginAsync(login);

    // Assert
    result.Should().NotBeNull();
    result.AccessToken.Should().Be(token);
    result.RefreshToken.Should().Be(refreshToken);
    result.ExpiresIn.Should().Be(expiresIn);
    result.User.Should().NotBeNull();
    result.User.Id.Should().Be(user.Id);
  }

  #endregion

  #region RefreshTokenAsync Tests

  [Fact]
  public async Task RefreshTokenAsync_WithNullRefreshToken_ThrowsArgumentException()
  {
    // Act & Assert
    await Assert.ThrowsAsync<ArgumentException>(() =>
        _authService.RefreshTokenAsync(null!));
  }

  [Fact]
  public async Task RefreshTokenAsync_WithInvalidToken_ThrowsUnauthorizedAccessException()
  {
    // Arrange
    var refreshToken = new RefreshToken
    {
      AccessToken = "invalid.token",
      RefreshTokenValue = "refresh.value"
    };

    _mockSecurityService.Setup(x => x.GetUserIdFromToken(refreshToken.AccessToken))
        .Throws(new Exception("Invalid token"));

    // Act & Assert
    var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        _authService.RefreshTokenAsync(refreshToken));

    exception.Message.Should().Contain("Invalid token format");
  }

  [Fact]
  public async Task RefreshTokenAsync_WithInvalidRefreshTokenValue_ThrowsUnauthorizedAccessException()
  {
    // Arrange
    var refreshToken = new RefreshToken
    {
      AccessToken = "valid.token",
      RefreshTokenValue = "invalid.refresh.token"
    };
    var userId = 1;

    _mockSecurityService.Setup(x => x.GetUserIdFromToken(refreshToken.AccessToken))
        .Returns(userId);
    _mockSecurityService.Setup(x => x.ValidateRefreshToken(refreshToken.RefreshTokenValue))
        .Returns(false);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        _authService.RefreshTokenAsync(refreshToken));

    exception.Message.Should().Contain("Invalid or expired refresh token");
  }

  [Fact]
  public async Task RefreshTokenAsync_WithNonExistentUser_ThrowsUnauthorizedAccessException()
  {
    // Arrange
    var refreshToken = new RefreshToken
    {
      AccessToken = "valid.token",
      RefreshTokenValue = "valid.refresh.token"
    };
    var userId = 1;

    _mockSecurityService.Setup(x => x.GetUserIdFromToken(refreshToken.AccessToken))
        .Returns(userId);
    _mockSecurityService.Setup(x => x.ValidateRefreshToken(refreshToken.RefreshTokenValue))
        .Returns(true);
    _mockUserService.Setup(x => x.GetUserByIdAsync(userId))
        .ReturnsAsync((User?)null);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        _authService.RefreshTokenAsync(refreshToken));

    exception.Message.Should().Contain("User not found");
  }

  [Fact]
  public async Task RefreshTokenAsync_WithValidData_ReturnsNewAuthResponse()
  {
    // Arrange
    var refreshToken = new RefreshToken
    {
      AccessToken = "valid.token",
      RefreshTokenValue = "valid.refresh.token"
    };
    var userId = 1;
    var user = TestHelper.CreateValidUser();
    var newToken = "new.jwt.token";
    var newRefreshToken = "new.refresh.token";
    var expiresIn = 3600;

    _mockSecurityService.Setup(x => x.GetUserIdFromToken(refreshToken.AccessToken))
        .Returns(userId);
    _mockSecurityService.Setup(x => x.ValidateRefreshToken(refreshToken.RefreshTokenValue))
        .Returns(true);
    _mockUserService.Setup(x => x.GetUserByIdAsync(userId))
        .ReturnsAsync(user);
    _mockSecurityService.Setup(x => x.GenerateJwtToken(user))
        .Returns(newToken);
    _mockSecurityService.Setup(x => x.GenerateRefreshToken())
        .Returns(newRefreshToken);
    _mockSecurityService.Setup(x => x.GetTokenExpirationSeconds())
        .Returns(expiresIn);

    // Act
    var result = await _authService.RefreshTokenAsync(refreshToken);

    // Assert
    result.Should().NotBeNull();
    result.AccessToken.Should().Be(newToken);
    result.RefreshToken.Should().Be(newRefreshToken);
    result.ExpiresIn.Should().Be(expiresIn);
    result.User.Should().NotBeNull();
    result.User.Id.Should().Be(user.Id);
  }

  #endregion

  #region ValidateTokenAsync Tests

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task ValidateTokenAsync_WithEmptyToken_ThrowsUnauthorizedAccessException(string token)
  {
    // Act & Assert
    var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        _authService.ValidateTokenAsync(token));

    exception.Message.Should().Contain("Token is required");
  }

  [Fact]
  public async Task ValidateTokenAsync_WithInvalidToken_ThrowsUnauthorizedAccessException()
  {
    // Arrange
    var token = "invalid.jwt.token";

    _mockSecurityService.Setup(x => x.ValidateJwtToken(token))
        .Throws(new Exception("Invalid token"));

    // Act & Assert
    var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        _authService.ValidateTokenAsync(token));

    exception.Message.Should().Contain("Invalid token");
  }

  [Fact]
  public async Task ValidateTokenAsync_WithValidTokenButNonExistentUser_ThrowsUnauthorizedAccessException()
  {
    // Arrange
    var token = "valid.jwt.token";
    var userId = 1;

    _mockSecurityService.Setup(x => x.ValidateJwtToken(token))
        .Returns(userId);
    _mockUserService.Setup(x => x.GetUserByIdAsync(userId))
        .ReturnsAsync((User?)null);

    // Act & Assert
    var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
        _authService.ValidateTokenAsync(token));

    exception.Message.Should().Contain("User not found");
  }

  [Fact]
  public async Task ValidateTokenAsync_WithValidToken_ReturnsUser()
  {
    // Arrange
    var token = "valid.jwt.token";
    var userId = 1;
    var user = TestHelper.CreateValidUser();

    _mockSecurityService.Setup(x => x.ValidateJwtToken(token))
        .Returns(userId);
    _mockUserService.Setup(x => x.GetUserByIdAsync(userId))
        .ReturnsAsync(user);

    // Act
    var result = await _authService.ValidateTokenAsync(token);

    // Assert
    result.Should().NotBeNull();
    result.Id.Should().Be(user.Id);
    result.Email.Should().Be(user.Email);
    result.Username.Should().Be(user.Username);
  }

  #endregion
}
