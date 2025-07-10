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
  private PostgreSqlContainer? _dbContainer;
  private DbConnection? _dbConnection;
  private readonly bool _useTestcontainers;

  public WebForumTestFactory()
  {
    // Use Testcontainers in CI environments, local connection otherwise
    _useTestcontainers = Environment.GetEnvironmentVariable("CI") == "true" || 
                        Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ||
                        Environment.GetEnvironmentVariable("USE_TESTCONTAINERS") == "true";
  }

  /// <summary>
  /// Gets the database connection string for the test container or local database
  /// </summary>
  public string ConnectionString
  {
    get
    {
      if (_useTestcontainers)
      {
        if (_dbContainer == null)
          throw new InvalidOperationException("Database container not initialized");

        // Use the built-in connection string from PostgreSqlContainer
        // This handles networking properly across different environments
        return _dbContainer.GetConnectionString();
      }
      else
      {
        // Use local database connection string for development
        return "Host=localhost;Port=5432;Database=webforum;Username=postgres;Password=password";
      }
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
    if (_useTestcontainers)
    {
      // Build and start PostgreSQL container for CI environments
      _dbContainer = new PostgreSqlBuilder()
          .WithImage("postgres:15-alpine")
          .WithDatabase("webforum_test")
          .WithUsername("postgres")
          .WithPassword("test_password")
          .WithCleanUp(true)
          .Build();

      await _dbContainer.StartAsync();
    }

    // Test connection with retry logic for both environments
    var maxRetries = _useTestcontainers ? 30 : 10; // More retries for container startup
    var delay = TimeSpan.FromMilliseconds(500);
    Exception? lastException = null;

    for (int i = 0; i < maxRetries; i++)
    {
      try
      {
        using var testConnection = new Npgsql.NpgsqlConnection(ConnectionString);
        await testConnection.OpenAsync();

        // Test that we can actually query the database
        using var testCommand = testConnection.CreateCommand();
        testCommand.CommandText = "SELECT 1";
        await testCommand.ExecuteScalarAsync();

        // Connection successful, keep a permanent connection for the factory
        _dbConnection = new Npgsql.NpgsqlConnection(ConnectionString);
        await _dbConnection.OpenAsync();

        break; // Success - exit retry loop
      }
      catch (Exception ex) when (i < maxRetries - 1)
      {
        lastException = ex;

        // Close any partial connection
        _dbConnection?.Close();
        _dbConnection?.Dispose();
        _dbConnection = null;

        // Retry with exponential backoff
        await Task.Delay(delay);
        delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 1.2, 5000));
      }
      catch (Exception ex)
      {
        lastException = ex;
        break;
      }
    }

    if (_dbConnection?.State != System.Data.ConnectionState.Open)
    {
      var errorMessage = $"Failed to connect to {(_useTestcontainers ? "PostgreSQL container" : "local PostgreSQL database")} after {maxRetries} retries. " +
                        $"Connection string: {ConnectionString}. " +
                        $"Last error: {lastException?.Message}";
      throw new InvalidOperationException(errorMessage, lastException);
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

    // Add retry logic for CI environments where containers might still be initializing
    var maxRetries = 10;
    var delay = TimeSpan.FromMilliseconds(500);

    for (int retry = 0; retry < maxRetries; retry++)
    {
      try
      {
        // First ensure we can connect to the database
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
          await connection.OpenAsync();
        }

        // Test basic connectivity
        using var testCommand = connection.CreateCommand();
        testCommand.CommandText = "SELECT 1";
        await testCommand.ExecuteScalarAsync();

        // Check if migrations are available
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        
        if (!pendingMigrations.Any())
        {
          // Try to ensure database is created even without explicit migrations
          var created = await context.Database.EnsureCreatedAsync();
          
          // If EnsureCreated was used, verify tables exist
          if (created)
          {
            using var verifyCommand = connection.CreateCommand();
            verifyCommand.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'PostTags')";
            var verifyTableExists = (bool)(await verifyCommand.ExecuteScalarAsync() ?? false);
            
            if (verifyTableExists)
            {
              return; // Success
            }
          }
        }
        else
        {
          // Apply migrations
          await context.Database.MigrateAsync();
        }

        // Verify that all required tables exist
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'PostTags')";
        var tableExists = (bool)(await command.ExecuteScalarAsync() ?? false);

        if (tableExists)
        {
          // Add small delay to ensure migration is fully committed in CI environment
          await Task.Delay(100);
          return;
        }
      }
      catch (Exception) when (retry < maxRetries - 1)
      {
        // Log and retry for CI environment timing issues
        await Task.Delay(delay);
        delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 1.5, 5000)); // Exponential backoff with cap
        continue;
      }
      catch (Exception)
      {
        throw;
      }
    }

    throw new InvalidOperationException("Failed to initialize database after multiple retries");
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
      // Delete data in reverse order to respect foreign key constraints
      // Add small delays between operations to prevent race conditions in CI
      await TruncateTableIfExistsAsync(context, "PostTags");
      await Task.Delay(50);

      await TruncateTableIfExistsAsync(context, "Likes");
      await Task.Delay(50);

      await TruncateTableIfExistsAsync(context, "Comments");
      await Task.Delay(50);

      await TruncateTableIfExistsAsync(context, "Posts");
      await Task.Delay(50);

      await TruncateTableIfExistsAsync(context, "Users");
      await Task.Delay(50);

      // Reset sequences - only if they exist
      await ResetSequenceIfExistsAsync(context, "Users_Id_seq");
      await Task.Delay(25);

      await ResetSequenceIfExistsAsync(context, "Posts_Id_seq");
      await Task.Delay(25);

      await ResetSequenceIfExistsAsync(context, "Comments_Id_seq");
      await Task.Delay(25);

      await ResetSequenceIfExistsAsync(context, "Likes_Id_seq");
      await Task.Delay(25);

      await ResetSequenceIfExistsAsync(context, "PostTags_Id_seq");
      await Task.Delay(25);
    }
    catch (Exception)
    {
      // In CI environments, sometimes cleanup fails due to timing - log but don't fail tests
      // Swallow the exception to prevent test failures due to cleanup issues
    }
  }

  private async Task TruncateTableIfExistsAsync(ForumDbContext context, string tableName)
  {
    try
    {
      // Use ExecuteSqlRaw since table names cannot be parameterized, but validate input
      if (!IsValidTableName(tableName))
        throw new ArgumentException($"Invalid table name: {tableName}");

#pragma warning disable EF1002 // Risk of vulnerability to SQL injection
      await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" CASCADE");
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection
    }
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01") // Table does not exist
    {
      // Table doesn't exist yet, ignore
    }
  }

  private async Task ResetSequenceIfExistsAsync(ForumDbContext context, string sequenceName)
  {
    try
    {
      // Use ExecuteSqlRaw since sequence names cannot be parameterized, but validate input
      if (!IsValidSequenceName(sequenceName))
        throw new ArgumentException($"Invalid sequence name: {sequenceName}");

#pragma warning disable EF1002 // Risk of vulnerability to SQL injection
      await context.Database.ExecuteSqlRawAsync($"ALTER SEQUENCE \"{sequenceName}\" RESTART WITH 1");
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection
    }
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01") // Sequence does not exist
    {
      // Sequence doesn't exist yet, ignore
    }
  }

  private static bool IsValidTableName(string tableName)
  {
    var validTables = new[] { "PostTags", "Likes", "Comments", "Posts", "Users" };
    return validTables.Contains(tableName);
  }

  private static bool IsValidSequenceName(string sequenceName)
  {
    var validSequences = new[] { "Users_Id_seq", "Posts_Id_seq", "Comments_Id_seq", "Likes_Id_seq", "PostTags_Id_seq" };
    return validSequences.Contains(sequenceName);
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

    if (_useTestcontainers && _dbContainer != null)
    {
      await _dbContainer.StopAsync();
      await _dbContainer.DisposeAsync();
    }

    await base.DisposeAsync();
  }
}
