using Microsoft.EntityFrameworkCore;
using ably_rest_apis.src.Api.Middlewares;
using ably_rest_apis.src.Application.Abstractions.Messaging;
using ably_rest_apis.src.Application.Abstractions.Services;
using ably_rest_apis.src.Features.Sessions;
using ably_rest_apis.src.Infrastructure.Messaging;
using ably_rest_apis.src.Infrastructure.Persistence.DbContext;
using ably_rest_apis.src.Infrastructure.Zoom;

var builder = WebApplication.CreateBuilder(args);

// Load configuration from correct path
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("src/Api/appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"src/Api/appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add services to the container

// Database Context - MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Debug: Log connection string (remove in production)
Console.WriteLine($"🔧 Connection String Loaded: {(string.IsNullOrEmpty(connectionString) ? "❌ EMPTY!" : "✅ Found")}");
if (!string.IsNullOrEmpty(connectionString))
{
    // Mask password for security in logs
    var maskedCs = System.Text.RegularExpressions.Regex.Replace(connectionString, @"Password=([^;]*)", "Password=***");
    Console.WriteLine($"   → {maskedCs}");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString!, ServerVersion.AutoDetect(connectionString!)));

// Ably Publisher
builder.Services.AddSingleton<IAblyPublisher, AblyPublisher>();

// Zoom Video SDK JWT Service
builder.Services.AddSingleton<IZoomJwtService, ZoomJwtService>();

// Application Services
builder.Services.AddScoped<ISessionService, SessionService>();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// OpenAPI / Swagger
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline

// Exception handling middleware
app.UseExceptionHandling();

// Swagger (always enabled for PoC)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ably Exam Session API v1");
    c.RoutePrefix = "swagger";
});

// Map OpenAPI endpoint
app.MapOpenApi();

// CORS
app.UseCors("AllowAll");

// Routing
app.UseRouting();

// Endpoints
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Service = "Ably Exam Session API"
});

// API info endpoint at root
app.MapGet("/", () => new
{
    Name = "Ably Exam Session API",
    Version = "1.0",
    Documentation = "/swagger",
    Health = "/health"
});

// Auto-migrate database on startup (for development)
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();
        Console.WriteLine("✅ Database connection successful - tables created");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Database connection failed: {ex.Message}");
    Console.WriteLine("   Please ensure MySQL is running and the connection string is correct.");
    Console.WriteLine("   The API will start, but database operations will fail until MySQL is available.");
}

Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║         Ably Exam Session Management API                      ║
║                                                               ║
║  📚 Swagger UI: http://localhost:5000/swagger                 ║
║  🔧 Health Check: http://localhost:5000/health                ║
║                                                               ║
║  📡 Events are published to Ably channel: session:{sessionId}  ║
╚══════════════════════════════════════════════════════════════╝
");

app.Run();
