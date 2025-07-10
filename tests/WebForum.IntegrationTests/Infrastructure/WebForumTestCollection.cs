using Xunit;

namespace WebForum.IntegrationTests.Infrastructure;

/// <summary>
/// Defines a test collection to ensure tests run in isolation
/// and prevent parallel execution issues in CI environments
/// </summary>
[CollectionDefinition("WebForum Integration Tests")]
public class WebForumTestCollection : ICollectionFixture<WebForumTestFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
