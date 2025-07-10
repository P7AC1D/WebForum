using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Text;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WebForum.Api.Data;

namespace WebForum.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for integration tests with PostgreSQL test container
/// </summary>
public class WebForumTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
  private IContainer? _dbContainer;
  private DbConnection? _dbConnection;

  /// <summary>
  /// Gets the database connection string for the test container
  /// </summary>
  public string ConnectionString
  {
    get
    {
      if (_dbContainer == null)
        throw new InvalidOperationException("Database container not initialized");

      var host = _dbContainer.Hostname;
      var port = _dbContainer.GetMappedPublicPort(5432);
      return $"Host={host};Port={port};Database=webforum_test;Username=postgres;Password=test_password";
    }
  }

  /// <summary>
  /// Gets the HTTP client configured for testing
  /// </summary>
  public HttpClient TestClient => CreateClient();

  /// <summary>
  /// Gets the service scope for accessing services
  /// </summary>
  public IServiceScope CreateScope() => Services.CreateScope();

  /// <summary>
  /// Gets the database context for direct database operations
  /// </summary>
  public ForumDbContext GetDbContext()
  {
    var scope = CreateScope();
    return scope.ServiceProvider.GetRequiredService<ForumDbContext>();
  }

  /// <summary>
  /// Initialize the test factory and start containers
  /// </summary>
  public async Task InitializeAsync()
  {
    // Build and start PostgreSQL container without any wait strategy to avoid exec issues
    _dbContainer = new ContainerBuilder()
        .WithImage("postgres:15-alpine")
        .WithEnvironment("POSTGRES_DB", "webforum_test")
        .WithEnvironment("POSTGRES_USER", "postgres")
        .WithEnvironment("POSTGRES_PASSWORD", "test_password")
        .WithPortBinding(0, 5432) // Use random available port
        .WithCleanUp(true)
        .Build();

    await _dbContainer.StartAsync();

    // Test connection with retry logic
    var maxRetries = 15;
    var delay = TimeSpan.FromSeconds(2);

    for (int i = 0; i < maxRetries; i++)
    {
      try
      {
        _dbConnection = new Npgsql.NpgsqlConnection(ConnectionString);
        await _dbConnection.OpenAsync();
        break; // Success - exit retry loop
      }
      catch (Exception) when (i < maxRetries - 1)
      {
        // Log and retry
        await Task.Delay(delay);
      }
    }

    if (_dbConnection?.State != System.Data.ConnectionState.Open)
    {
      throw new InvalidOperationException("Failed to connect to PostgreSQL container after retries");
    }
  }

  /// <summary>
  /// Configure services for testing
  /// </summary>
  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.ConfigureAppConfiguration((context, config) =>
    {
      // Clear existing configuration
      config.Sources.Clear();

      // Add test configuration
      config.AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["ConnectionStrings:DefaultConnection"] = ConnectionString,
        ["JwtSettings:SecretKey"] = "TestSecretKeyThatIsAtLeast32CharactersLongForTesting!",
        ["JwtSettings:Issuer"] = "WebForumTestApi",
        ["JwtSettings:Audience"] = "WebForumTestUsers",
        ["JwtSettings:ExpirationInMinutes"] = "60",
        ["Logging:LogLevel:Default"] = "Warning",
        ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
        ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
      });
    });

    builder.ConfigureServices(services =>
    {
      // Remove the existing DbContext registration
      var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ForumDbContext>));
      if (descriptor != null)
      {
        services.Remove(descriptor);
      }

      // Add test database context
      services.AddDbContext<ForumDbContext>(options =>
          {
          options.UseNpgsql(ConnectionString);
          options.EnableSensitiveDataLogging();
          options.EnableDetailedErrors();
        });

      // Reconfigure JWT Bearer authentication for test environment
      services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
          {
          var testSecretKey = "TestSecretKeyThatIsAtLeast32CharactersLongForTesting!";
          var testIssuer = "WebForumTestApi";
          var testAudience = "WebForumTestUsers";

          options.TokenValidationParameters = new TokenValidationParameters
          {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = testIssuer,
            ValidAudience = testAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(testSecretKey)),
            ClockSkew = TimeSpan.Zero
          };
        });

      // Configure logging for tests
      services.AddLogging(builder =>
          {
          builder.ClearProviders();
          builder.AddConsole();
          builder.SetMinimumLevel(LogLevel.Warning);
        });
    });

    builder.UseEnvironment("Testing");
  }

  /// <summary>
  /// Ensure database schema is created and up to date
  /// </summary>
  public async Task EnsureDatabaseCreatedAsync()
  {
    using var scope = CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ForumDbContext>();

    try
    {
      // First ensure database exists and is accessible
      await context.Database.EnsureCreatedAsync();
      
      // Apply any pending migrations
      await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException($"Failed to initialize database: {ex.Message}", ex);
    }
  }

  /// <summary>
  /// Clean up database by deleting all data but keeping schema
  /// </summary>
  public async Task CleanDatabaseAsync()
  {
    using var scope = CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ForumDbContext>();

    try
    {
      // Simple approach: just delete all data in the correct order
      // This avoids the complex table existence checks that were causing issues
      
      // Delete data in reverse order to respect foreign key constraints
      await context.Database.ExecuteSqlRawAsync("DELETE FROM \"PostTags\"");
      await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Likes\"");
      await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Comments\"");
      await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Posts\"");
      await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\"");

      // Reset sequences to start from 1
      await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Users_Id_seq\" RESTART WITH 1");
      await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Posts_Id_seq\" RESTART WITH 1");
      await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Comments_Id_seq\" RESTART WITH 1");
      await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Likes_Id_seq\" RESTART WITH 1");
      await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"PostTags_Id_seq\" RESTART WITH 1");
    }
    catch (Exception)
    {
      // If cleanup fails, try to ensure database is created first
      await EnsureDatabaseCreatedAsync();
      
      // Try cleanup again
      await context.Database.ExecuteSqlRawAsync("DELETE FROM \"PostTags\"");
      await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Likes\"");
      await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Comments\"");
      await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Posts\"");
      await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\"");

      await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Users_Id_seq\" RESTART WITH 1");
      await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Posts_Id_seq\" RESTART WITH 1");
      await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Comments_Id_seq\" RESTART WITH 1");
      await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"Likes_Id_seq\" RESTART WITH 1");
      await context.Database.ExecuteSqlRawAsync("ALTER SEQUENCE \"PostTags_Id_seq\" RESTART WITH 1");
    }
  }

  /// <summary>
  /// Reset database to clean state for next test
  /// </summary>
  public async Task ResetDatabaseAsync()
  {
    await CleanDatabaseAsync();
    await EnsureDatabaseCreatedAsync();
  }

  /// <summary>
  /// Clean up resources
  /// </summary>
  public new async Task DisposeAsync()
  {
    _dbConnection?.Close();
    _dbConnection?.Dispose();

    if (_dbContainer != null)
    {
      await _dbContainer.StopAsync();
      await _dbContainer.DisposeAsync();
    }

    await base.DisposeAsync();
  }
}
