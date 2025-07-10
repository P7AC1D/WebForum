using FluentAssertions;
using WebForum.IntegrationTests.Base;
using WebForum.IntegrationTests.Infrastructure;

namespace WebForum.IntegrationTests.Examples;

/// <summary>
/// Simple example test to demonstrate the integration testing framework
/// </summary>
public class BasicIntegrationTest : IntegrationTestBase
{
  public BasicIntegrationTest(WebForumTestFactory factory) : base(factory)
  {
  }

  [Fact]
  public async Task GetPosts_ShouldReturnEmptyList_WhenNoPosts()
  {
    // Arrange
    await InitializeTestAsync();

    // Act
    var response = await Client.GetAsync("/api/posts");

    // Assert
    AssertSuccessStatusCode(response);
    var content = await response.Content.ReadAsStringAsync();
    content.Should().NotBeEmpty();
    content.Should().Contain("\"totalCount\":0");
  }

  [Fact]
  public async Task Factory_ShouldProvideWorkingDbContext()
  {
    // Arrange
    await InitializeTestAsync();

    // Act
    using var dbContext = GetDbContext();
    var canConnect = await dbContext.Database.CanConnectAsync();

    // Assert
    canConnect.Should().BeTrue();
  }

  [Fact]
  public async Task Authentication_ShouldWork()
  {
    // Arrange
    await InitializeTestAsync();
    var authClient = CreateAuthenticatedClient(1, "testuser");

    // Act
    var response = await authClient.GetAsync("/api/posts");

    // Assert
    AssertSuccessStatusCode(response);
  }
}
