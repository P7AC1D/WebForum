using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebForum.Api.Data;
using WebForum.DataSeeder;

/// <summary>
/// Console application for seeding the Web Forum database with realistic test data
/// This provides the "datastore of test/dummy data" required by the assessment
/// </summary>
class Program
{
  static async Task Main(string[] args)
  {
    Console.WriteLine("🌱 Web Forum Database Seeder");
    Console.WriteLine("============================");
    Console.WriteLine();

    try
    {
      // Parse command-line arguments
      var userCount = GetArgValue(args, "--users", 10);
      var postCount = GetArgValue(args, "--posts", 25);
      var commentCount = GetArgValue(args, "--comments", 50);
      var likeCount = GetArgValue(args, "--likes", 75);
      var force = args.Contains("--force");

      // Setup configuration and services
      var host = CreateHost();
      using var scope = host.Services.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<ForumDbContext>();

      // Ensure database exists and is up to date
      Console.WriteLine("🔍 Checking database connection...");
      await context.Database.MigrateAsync();

      // Check if data already exists
      var existingUsers = await context.Users.CountAsync();
      if (existingUsers > 0 && !force)
      {
        Console.WriteLine($"⚠️  Database already contains {existingUsers} users.");
        Console.WriteLine("   Use --force to overwrite existing data.");
        Console.WriteLine("   Example: dotnet run --force --users 15 --posts 50");
        return;
      }

      Console.WriteLine($"📊 Seeding parameters:");
      Console.WriteLine($"├── Users: {userCount}");
      Console.WriteLine($"├── Posts: {postCount}");
      Console.WriteLine($"├── Comments: {commentCount}");
      Console.WriteLine($"└── Likes: {likeCount}");
      Console.WriteLine();

      if (existingUsers > 0)
      {
        Console.WriteLine("⚠️  Force mode enabled - existing data will be replaced");
        Console.WriteLine();
      }

      // Perform seeding
      await DatabaseSeeder.SeedAsync(context, userCount, postCount, commentCount, likeCount);

      Console.WriteLine();
      Console.WriteLine("🎉 Database seeding completed successfully!");
      Console.WriteLine();
      Console.WriteLine("📋 Test Account Information:");
      Console.WriteLine("├── Moderator Account:");
      Console.WriteLine("│   ├── Username: moderator");
      Console.WriteLine("│   ├── Email: moderator@webforum.com");
      Console.WriteLine("│   └── Password: password123");
      Console.WriteLine("└── Regular User Account:");
      Console.WriteLine("    ├── Username: testuser");
      Console.WriteLine("    ├── Email: user@webforum.com");
      Console.WriteLine("    └── Password: password123");
      Console.WriteLine();
      Console.WriteLine("🔗 API Documentation: https://localhost:7094/scalar/v1");
      Console.WriteLine("🗄️  Database Admin: http://localhost:5050");
    }
    catch (Exception ex)
    {
      Console.WriteLine($"❌ Seeding failed: {ex.Message}");
      if (args.Contains("--verbose"))
      {
        Console.WriteLine(ex.StackTrace);
      }
      Environment.Exit(1);
    }
  }

  static IHost CreateHost()
  {
    var builder = Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration((context, config) =>
        {
          config.AddJsonFile("appsettings.json", optional: false);
        })
        .ConfigureServices((context, services) =>
        {
          var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
          services.AddDbContext<ForumDbContext>(options =>
              options.UseNpgsql(connectionString));
        });

    return builder.Build();
  }

  static int GetArgValue(string[] args, string argName, int defaultValue)
  {
    var index = Array.IndexOf(args, argName);
    if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int value))
      return value;
    return defaultValue;
  }
}
