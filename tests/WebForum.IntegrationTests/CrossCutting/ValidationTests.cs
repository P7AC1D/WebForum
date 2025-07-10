using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;
using WebForum.Api.Models.Response;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;

namespace WebForum.IntegrationTests.CrossCutting;

/// <summary>
/// Integration tests for data validation across all API endpoints
/// </summary>
public class ValidationTests : IntegrationTestBase
{

  [Fact]
  public async Task Registration_ShouldValidateRequiredFields()
  {
    // Arrange
    var invalidRequests = new[]
    {
            new RegistrationRequest { Username = "", Email = "test@test.com", Password = "Test123!" },
            new RegistrationRequest { Username = "testuser", Email = "", Password = "Test123!" },
            new RegistrationRequest { Username = "testuser", Email = "test@test.com", Password = "" },
            new RegistrationRequest { Username = null!, Email = "test@test.com", Password = "Test123!" },
            new RegistrationRequest { Username = "testuser", Email = null!, Password = "Test123!" },
            new RegistrationRequest { Username = "testuser", Email = "test@test.com", Password = null! }
        };

    // Act & Assert
    foreach (var request in invalidRequests)
    {
      var response = await Client.PostAsJsonAsync("/api/auth/register", request);
      response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

  }

  [Fact]
  public async Task Registration_ShouldValidateEmailFormat()
  {
    // Arrange
    var invalidEmails = new[]
    {
            "invalid-email",
            "invalid@",
            "@invalid.com",
            "invalid@invalid",
            "invalid.email",
            "invalid@.com",
            "invalid@com.",
            "invalid..email@test.com"
        };

    // Act & Assert
    foreach (var email in invalidEmails)
    {
      var request = new RegistrationRequest
      {
        Username = "testuser",
        Email = email,
        Password = "Test123!"
      };

      var response = await Client.PostAsJsonAsync("/api/auth/register", request);
      response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

  }

  [Fact]
  public async Task Registration_ShouldValidatePasswordRequirements()
  {
    // Arrange
    var weakPasswords = new[]
    {
            "weak",           // Too short (4 characters)
            "short",          // Too short (5 characters)
            "1234567"         // Too short (7 characters)
        };

    // Act & Assert
    foreach (var password in weakPasswords)
    {
      var request = new RegistrationRequest
      {
        Username = "testuser",
        Email = "test@test.com",
        Password = password
      };

      var response = await Client.PostAsJsonAsync("/api/auth/register", request);
      response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Test that an 8-character password is accepted
    var validRequest = new RegistrationRequest
    {
      Username = "validuser",
      Email = "valid@test.com",
      Password = "12345678"  // Exactly 8 characters
    };

    var validResponse = await Client.PostAsJsonAsync("/api/auth/register", validRequest);
    validResponse.StatusCode.Should().Be(HttpStatusCode.Created);

  }

  [Fact]
  public async Task CreatePost_ShouldValidateRequiredFields()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username, UserRoles.User);

    var invalidRequests = new[]
    {
            new CreatePostRequest { Title = "", Content = "Valid content" },
            new CreatePostRequest { Title = "Valid title", Content = "" },
            new CreatePostRequest { Title = null!, Content = "Valid content" },
            new CreatePostRequest { Title = "Valid title", Content = null! }
        };

    // Act & Assert
    foreach (var request in invalidRequests)
    {
      var response = await authenticatedClient.PostAsJsonAsync("/api/posts", request);
      response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

  }

  [Fact]
  public async Task CreatePost_ShouldValidateFieldLengths()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username, UserRoles.User);

    var tooLongTitle = new string('A', 201); // Assuming max title length is 200
    var tooLongContent = new string('B', 10001); // Assuming max content length is 10000

    var invalidRequests = new[]
    {
            new CreatePostRequest { Title = tooLongTitle, Content = "Valid content" },
            new CreatePostRequest { Title = "Valid title", Content = tooLongContent }
        };

    // Act & Assert
    foreach (var request in invalidRequests)
    {
      var response = await authenticatedClient.PostAsJsonAsync("/api/posts", request);
      response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

  }

  [Fact]
  public async Task CreateComment_ShouldValidateRequiredFields()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username, UserRoles.User);

    // Create a post first
    var postRequest = new CreatePostRequest
    {
      Title = "Test Post",
      Content = "Test content"
    };

    var postResponse = await authenticatedClient.PostAsJsonAsync("/api/posts", postRequest);
    var post = await postResponse.Content.ReadFromJsonAsync<PostResponse>();

    var invalidRequests = new[]
    {
            new CreateCommentRequest { Content = "" },
            new CreateCommentRequest { Content = null! }
        };

    // Act & Assert
    foreach (var request in invalidRequests)
    {
      var response = await authenticatedClient.PostAsJsonAsync($"/api/posts/{post!.Id}/comments", request);
      response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Test commenting on non-existent post
    var validRequest = new CreateCommentRequest { Content = "Valid content" };
    var nonExistentPostResponse = await authenticatedClient.PostAsJsonAsync("/api/posts/99999/comments", validRequest);
    nonExistentPostResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

  }

  [Fact]
  public async Task Api_ShouldRejectMalformedJson()
  {
    // Arrange
    var malformedJsons = new[]
    {
            "{invalid json}",
            "{'single quotes': 'not valid json'}",
            "{\"unclosed\": \"string}",
            "{\"trailing\": \"comma\",}",
            "{\"duplicate\": \"key\", \"duplicate\": \"value\"}",
            "null",
            "undefined"
        };

    // Act & Assert
    foreach (var json in malformedJsons)
    {
      var content = new StringContent(json, Encoding.UTF8, "application/json");
      var response = await Client.PostAsync("/api/auth/register", content);

      // Should return BadRequest for malformed JSON
      response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

  }

  [Fact]
  public async Task Api_ShouldRejectOversizedPayloads()
  {
    // Arrange
    // Create an extremely large payload (assuming there's a limit)
    var oversizedContent = new string('X', 1024 * 1024 * 2); // 2MB payload

    var oversizedRequest = new CreatePostRequest
    {
      Title = "Test",
      Content = oversizedContent
    };

    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username, UserRoles.User);

    // Act
    var response = await authenticatedClient.PostAsJsonAsync("/api/posts", oversizedRequest);

    // Assert
    // Should reject oversized payload (either BadRequest or PayloadTooLarge)
    response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge);

  }

  [Fact]
  public async Task Api_ShouldValidatePaginationParameters()
  {
    // Arrange
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username, UserRoles.User);

    var invalidPaginationUrls = new[]
    {
            "/api/posts?page=0",          // Page must be >= 1
            "/api/posts?page=-1",         // Page must be >= 1
            "/api/posts?pageSize=0",      // PageSize must be >= 1
            "/api/posts?pageSize=-1",     // PageSize must be >= 1
            "/api/posts?pageSize=101",    // PageSize might have upper limit
            "/api/posts?page=abc",        // Page must be numeric
            "/api/posts?pageSize=xyz"     // PageSize must be numeric
        };

    // Act & Assert
    foreach (var url in invalidPaginationUrls)
    {
      var response = await authenticatedClient.GetAsync(url);
      response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

  }

  [Fact]
  public async Task Api_ShouldValidateContentType()
  {
    // Arrange
    var validJson = """{"username": "test", "email": "test@test.com", "password": "Test123!", "confirmPassword": "Test123!"}""";

    var invalidContentTypes = new[]
    {
            "text/plain",
            "application/xml",
            "text/html",
            "application/form-data",
            "multipart/form-data"
        };

    // Act & Assert
    foreach (var contentType in invalidContentTypes)
    {
      var content = new StringContent(validJson, Encoding.UTF8, contentType);
      var response = await Client.PostAsync("/api/auth/register", content);

      // Should reject non-JSON content types for JSON endpoints
      // Note: ASP.NET Core returns BadRequest instead of UnsupportedMediaType for some invalid content types
      response.StatusCode.Should().BeOneOf(HttpStatusCode.UnsupportedMediaType, HttpStatusCode.BadRequest);
    }

  }

  /// <summary>
  /// Helper method to create a test user for validation tests
  /// </summary>
  private async Task<UserInfo> CreateTestUserAsync()
  {
    var registrationRequest = new RegistrationRequest
    {
      Username = $"user_{Guid.NewGuid():N}",
      Email = $"user_{Guid.NewGuid():N}@test.com",
      Password = "Test123!@#"
    };

    var response = await Client.PostAsJsonAsync("/api/auth/register", registrationRequest);
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
    return new UserInfo
    {
      Id = authResponse!.User.Id,
      Username = authResponse.User.Username,
      Email = authResponse.User.Email,
      Role = UserRoles.User.ToString()
    };
  }
}
