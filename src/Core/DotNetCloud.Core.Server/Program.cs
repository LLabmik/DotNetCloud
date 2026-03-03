using DotNetCloud.Core.Auth.Extensions;
using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Initialization;
using DotNetCloud.Core.Server.Extensions;
using DotNetCloud.Core.ServiceDefaults.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (logging, telemetry, health checks, CORS, etc.)
builder.AddDotNetCloudServiceDefaults();

// Add authentication and authorization
builder.Services.AddDotNetCloudAuth(builder.Configuration);

// Add database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDotNetCloudDbContext(connectionString);

// Add controllers
builder.Services.AddControllers();

// Add OpenAPI/Swagger
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply middleware
app.UseDotNetCloudMiddleware();

// Map health checks
app.MapDotNetCloudHealthChecks();

// Migrate and seed database
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await dbInitializer.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "v1"));
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Map OpenIddict endpoints
app.MapOpenIddictEndpoints();

// Map API controllers
app.MapControllers();

app.Run();
