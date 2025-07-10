using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WebForum.Api.Models;
using WebForum.Api.Services.Implementations;
using WebForum.UnitTests.Helpers;

namespace WebForum.UnitTests.Services;

/// <summary>
/// Unit tests for SecurityService - Critical security functionality
/// </summary>
public class SecurityServiceTests
{
  private readonly Mock<IConfiguration> _mockConfig;
  private readonly SecurityService _securityService;

  public SecurityServiceTests()
  {
    _mockConfig = TestHelper.CreateMockConfiguration();
    _securityService = new SecurityService(_mockConfig.Object);
  }

  #region Constructor Tests

  [Fact]
  public void Constructor_WithValidConfiguration_ShouldCreateInstance()
  {
    // Arrange & Act
    var service = new SecurityService(_mockConfig.Object);

    // Assert
    service.Should().NotBeNull();
  }

  [Fact]
  public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
  {
    // Arrange, Act & Assert
    var action = () => new SecurityService(null!);
    action.Should().Throw<ArgumentNullException>()
        .WithParameterName("configuration");
  }

  [Theory]
  [InlineData("SecretKey", "JWT SecretKey is not configured")]
  [InlineData("Issuer", "JWT Issuer is not configured")]
  [InlineData("Audience", "JWT Audience is not configured")]
  public void Constructor_WithMissingJwtSetting_ShouldThrowInvalidOperationException(
      string missingSetting, string expectedMessage)
  {
    // Arrange
    var mockConfig = new Mock<IConfiguration>();
    var mockJwtSection = new Mock<IConfigurationSection>();

    // Setup all settings except the missing one
    if (missingSetting != "SecretKey")
      mockJwtSection.Setup(x => x["SecretKey"]).Returns("TestSecretKey32CharactersLong!");
    if (missingSetting != "Issuer")
      mockJwtSection.Setup(x => x["Issuer"]).Returns("TestIssuer");
    if (missingSetting != "Audience")
      mockJwtSection.Setup(x => x["Audience"]).Returns("TestAudience");

    mockJwtSection.Setup(x => x["ExpirationInMinutes"]).Returns("60");
    mockConfig.Setup(x => x.GetSection("JwtSettings")).Returns(mockJwtSection.Object);

    // Act & Assert
    var action = () => new SecurityService(mockConfig.Object);
    action.Should().Throw<InvalidOperationException>()
        .WithMessage(expectedMessage);
  }

  [Fact]
  public void Constructor_WithInvalidExpirationMinutes_ShouldThrowInvalidOperationException()
  {
    // Arrange
    var mockConfig = new Mock<IConfiguration>();
    var mockJwtSection = new Mock<IConfigurationSection>();

    mockJwtSection.Setup(x => x["SecretKey"]).Returns("TestSecretKey32CharactersLong!");
    mockJwtSection.Setup(x => x["Issuer"]).Returns("TestIssuer");
    mockJwtSection.Setup(x => x["Audience"]).Returns("TestAudience");
    mockJwtSection.Setup(x => x["ExpirationInMinutes"]).Returns("invalid");

    mockConfig.Setup(x => x.GetSection("JwtSettings")).Returns(mockJwtSection.Object);

    // Act & Assert
    var action = () => new SecurityService(mockConfig.Object);
    action.Should().Throw<InvalidOperationException>()
        .WithMessage("JWT ExpirationInMinutes is not configured or invalid");
  }

  #endregion

  #region JWT Token Generation Tests

  [Fact]
  public void GenerateJwtToken_WithValidUser_ShouldReturnValidJwtToken()
  {
    // Arrange
    var user = TestHelper.CreateTestUser();

    // Act
    var token = _securityService.GenerateJwtToken(user);

    // Assert
    token.Should().NotBeNullOrEmpty();

    // Verify token structure
    var tokenHandler = new JwtSecurityTokenHandler();
    tokenHandler.CanReadToken(token).Should().BeTrue();

    var jwtToken = tokenHandler.ReadJwtToken(token);
    jwtToken.Claims.Should().Contain(c => c.Type == "nameid" && c.Value == user.Id.ToString());
    jwtToken.Claims.Should().Contain(c => c.Type == "unique_name" && c.Value == user.Username);
    jwtToken.Claims.Should().Contain(c => c.Type == "email" && c.Value == user.Email);
    jwtToken.Claims.Should().Contain(c => c.Type == "role" && c.Value == user.Role.ToString());
  }

  [Fact]
  public void GenerateJwtToken_WithNullUser_ShouldThrowArgumentNullException()
  {
    // Arrange, Act & Assert
    var action = () => _securityService.GenerateJwtToken(null!);
    action.Should().Throw<ArgumentNullException>()
        .WithParameterName("user");
  }

  [Theory]
  [InlineData(UserRoles.User)]
  [InlineData(UserRoles.Moderator)]
  public void GenerateJwtToken_WithDifferentRoles_ShouldIncludeCorrectRoleClaim(UserRoles role)
  {
    // Arrange
    var user = TestHelper.CreateTestUser(role: role);

    // Act
    var token = _securityService.GenerateJwtToken(user);

    // Assert
    var tokenHandler = new JwtSecurityTokenHandler();
    var jwtToken = tokenHandler.ReadJwtToken(token);
    jwtToken.Claims.Should().Contain(c => c.Type == "role" && c.Value == role.ToString());
  }

  #endregion

  #region JWT Token Validation Tests

  [Fact]
  public void ValidateJwtToken_WithValidToken_ShouldReturnUserId()
  {
    // Arrange
    var user = TestHelper.CreateTestUser();
    var token = _securityService.GenerateJwtToken(user);

    // Act
    var result = _securityService.ValidateJwtToken(token);

    // Assert
    result.Should().Be(user.Id);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void ValidateJwtToken_WithInvalidToken_ShouldThrowUnauthorizedAccessException(string invalidToken)
  {
    // Arrange, Act & Assert
    var action = () => _securityService.ValidateJwtToken(invalidToken);
    action.Should().Throw<UnauthorizedAccessException>()
        .WithMessage("Token is required");
  }

  [Fact]
  public void ValidateJwtToken_WithNullToken_ShouldThrowUnauthorizedAccessException()
  {
    // Arrange, Act & Assert
    var action = () => _securityService.ValidateJwtToken(null!);
    action.Should().Throw<UnauthorizedAccessException>()
        .WithMessage("Token is required");
  }

  [Fact]
  public void ValidateJwtToken_WithMalformedToken_ShouldThrowUnauthorizedAccessException()
  {
    // Arrange
    var malformedToken = "invalid.token.format";

    // Act & Assert
    var action = () => _securityService.ValidateJwtToken(malformedToken);
    action.Should().Throw<UnauthorizedAccessException>()
        .And.Message.Should().Contain("Token validation failed");
  }

  [Fact]
  public void GetUserIdFromToken_WithValidToken_ShouldReturnUserId()
  {
    // Arrange
    var user = TestHelper.CreateTestUser();
    var token = _securityService.GenerateJwtToken(user);

    // Act
    var result = _securityService.GetUserIdFromToken(token);

    // Assert
    result.Should().Be(user.Id);
  }

  #endregion

  #region Password Hashing Tests

  [Fact]
  public void HashPassword_WithValidPassword_ShouldReturnHashedPassword()
  {
    // Arrange
    var password = "TestPassword123!";

    // Act
    var hashedPassword = _securityService.HashPassword(password);

    // Assert
    hashedPassword.Should().NotBeNullOrEmpty();
    hashedPassword.Should().NotBe(password); // Should be different from original
    hashedPassword.Should().StartWith("$2a$"); // BCrypt format
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void HashPassword_WithInvalidPassword_ShouldThrowArgumentException(string invalidPassword)
  {
    // Arrange, Act & Assert
    var action = () => _securityService.HashPassword(invalidPassword);
    action.Should().Throw<ArgumentException>()
        .WithParameterName("password")
        .WithMessage("Password cannot be null or empty*");
  }

  [Fact]
  public void HashPassword_WithNullPassword_ShouldThrowArgumentException()
  {
    // Arrange, Act & Assert
    var action = () => _securityService.HashPassword(null!);
    action.Should().Throw<ArgumentException>()
        .WithParameterName("password")
        .WithMessage("Password cannot be null or empty*");
  }

  [Fact]
  public void HashPassword_SamePasswordTwice_ShouldReturnDifferentHashes()
  {
    // Arrange
    var password = "TestPassword123!";

    // Act
    var hash1 = _securityService.HashPassword(password);
    var hash2 = _securityService.HashPassword(password);

    // Assert
    hash1.Should().NotBe(hash2); // BCrypt uses salt, so hashes should be different
  }

  #endregion

  #region Password Verification Tests

  [Fact]
  public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
  {
    // Arrange
    var password = "TestPassword123!";
    var hash = _securityService.HashPassword(password);

    // Act
    var result = _securityService.VerifyPassword(password, hash);

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
  {
    // Arrange
    var password = "TestPassword123!";
    var wrongPassword = "WrongPassword456!";
    var hash = _securityService.HashPassword(password);

    // Act
    var result = _securityService.VerifyPassword(wrongPassword, hash);

    // Assert
    result.Should().BeFalse();
  }

  [Theory]
  [InlineData("", "validhash")]
  [InlineData(" ", "validhash")]
  [InlineData("validpassword", "")]
  [InlineData("validpassword", " ")]
  public void VerifyPassword_WithInvalidInputs_ShouldReturnFalse(string password, string hash)
  {
    // Arrange, Act & Assert
    var result = _securityService.VerifyPassword(password, hash);
    result.Should().BeFalse();
  }

  [Fact]
  public void VerifyPassword_WithNullPassword_ShouldReturnFalse()
  {
    // Arrange, Act & Assert
    var result = _securityService.VerifyPassword(null!, "validhash");
    result.Should().BeFalse();
  }

  [Fact]
  public void VerifyPassword_WithNullHash_ShouldReturnFalse()
  {
    // Arrange, Act & Assert
    var result = _securityService.VerifyPassword("validpassword", null!);
    result.Should().BeFalse();
  }

  [Fact]
  public void VerifyPassword_WithMalformedHash_ShouldReturnFalse()
  {
    // Arrange
    var password = "TestPassword123!";
    var malformedHash = "not-a-valid-bcrypt-hash";

    // Act
    var result = _securityService.VerifyPassword(password, malformedHash);

    // Assert
    result.Should().BeFalse();
  }

  #endregion

  #region Refresh Token Tests

  [Fact]
  public void GenerateRefreshToken_ShouldReturnValidBase64String()
  {
    // Act
    var refreshToken = _securityService.GenerateRefreshToken();

    // Assert
    refreshToken.Should().NotBeNullOrEmpty();

    // Should be valid base64
    var action = () => Convert.FromBase64String(refreshToken);
    action.Should().NotThrow();

    // Should be 64 bytes when decoded
    var bytes = Convert.FromBase64String(refreshToken);
    bytes.Length.Should().Be(64);
  }

  [Fact]
  public void GenerateRefreshToken_MultipleCallsShouldReturnDifferentTokens()
  {
    // Act
    var token1 = _securityService.GenerateRefreshToken();
    var token2 = _securityService.GenerateRefreshToken();

    // Assert
    token1.Should().NotBe(token2);
  }

  [Fact]
  public void ValidateRefreshToken_WithValidToken_ShouldReturnTrue()
  {
    // Arrange
    var refreshToken = _securityService.GenerateRefreshToken();

    // Act
    var result = _securityService.ValidateRefreshToken(refreshToken);

    // Assert
    result.Should().BeTrue();
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void ValidateRefreshToken_WithInvalidToken_ShouldReturnFalse(string invalidToken)
  {
    // Arrange, Act & Assert
    var result = _securityService.ValidateRefreshToken(invalidToken);
    result.Should().BeFalse();
  }

  [Fact]
  public void ValidateRefreshToken_WithNullToken_ShouldReturnFalse()
  {
    // Arrange, Act & Assert
    var result = _securityService.ValidateRefreshToken(null!);
    result.Should().BeFalse();
  }

  [Fact]
  public void ValidateRefreshToken_WithMalformedBase64_ShouldReturnFalse()
  {
    // Arrange
    var malformedToken = "not-valid-base64!@#";

    // Act
    var result = _securityService.ValidateRefreshToken(malformedToken);

    // Assert
    result.Should().BeFalse();
  }

  [Fact]
  public void ValidateRefreshToken_WithWrongLength_ShouldReturnFalse()
  {
    // Arrange - Create a valid base64 string but wrong length
    var wrongLengthBytes = new byte[32]; // Should be 64
    var wrongLengthToken = Convert.ToBase64String(wrongLengthBytes);

    // Act
    var result = _securityService.ValidateRefreshToken(wrongLengthToken);

    // Assert
    result.Should().BeFalse();
  }

  #endregion

  #region Token Expiration Tests

  [Fact]
  public void GetTokenExpirationSeconds_ShouldReturnCorrectValue()
  {
    // Act
    var result = _securityService.GetTokenExpirationSeconds();

    // Assert
    result.Should().Be(60 * 60); // 60 minutes * 60 seconds
  }

  #endregion
}
