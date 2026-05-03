using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json.Serialization;
using SmartDocumentProcessingSystem.Configuration;
using SmartDocumentProcessingSystem.DatabaseContext;
using SmartDocumentProcessingSystem.Services;
using SmartDocumentProcessingSystem.Services.Processing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ProcessingOptions>(builder.Configuration.GetSection("Processing"));

var connectionString = NormalizePostgresConnectionString(
    builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is not configured."));

builder.Services.AddDbContext<SDPSContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<DocumentTextExtractor>();
builder.Services.AddScoped<DocumentParser>();
builder.Services.AddScoped<DocumentValidator>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SDPSContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();

static string NormalizePostgresConnectionString(string connectionString)
{
    if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        && !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return connectionString;
    }

    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':', 2);

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty
    };

    return builder.ConnectionString;
}
