using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using System.Text;
using WebForum.Api.Converters;
using WebForum.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure JSON to handle UserRoles enum with both string and integer support
        options.JsonSerializerOptions.Converters.Add(new UserRolesJsonConverter());
        // Allow case-insensitive property names
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Configure routing options for case-insensitive routes
builder.Services.Configure<RouteOptions>(options =>
{
  options.LowercaseUrls = true;
  options.LowercaseQueryStrings = true;
});

// Add Entity Framework
builder.Services.AddDbContext<ForumDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];

builder.Services.AddAuthentication(options =>
{
  options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
  options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
  options.TokenValidationParameters = new TokenValidationParameters
  {
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = issuer,
    ValidAudience = audience,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey!)),
    ClockSkew = TimeSpan.Zero
  };
});

builder.Services.AddAuthorization();

// Register services
builder.Services.AddScoped<WebForum.Api.Services.Interfaces.ISecurityService, WebForum.Api.Services.Implementations.SecurityService>();
builder.Services.AddScoped<WebForum.Api.Services.Interfaces.IAuthService, WebForum.Api.Services.Implementations.AuthService>();
builder.Services.AddScoped<WebForum.Api.Services.Interfaces.IUserService, WebForum.Api.Services.Implementations.UserService>();
builder.Services.AddScoped<WebForum.Api.Services.Interfaces.IPostService, WebForum.Api.Services.Implementations.PostService>();
builder.Services.AddScoped<WebForum.Api.Services.Interfaces.ICommentService, WebForum.Api.Services.Implementations.CommentService>();
builder.Services.AddScoped<WebForum.Api.Services.Interfaces.ILikeService, WebForum.Api.Services.Implementations.LikeService>();
builder.Services.AddScoped<WebForum.Api.Services.Interfaces.IModerationService, WebForum.Api.Services.Implementations.ModerationService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
  app.MapScalarApiReference(options =>
  {
    options.Title = "Web Forum API";
    options.OpenApiRoutePattern = "/openapi/v1.json";
  }).WithName("scalar-docs").ExcludeFromDescription();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
