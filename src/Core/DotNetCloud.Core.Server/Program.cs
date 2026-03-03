using DotNetCloud.Core.Auth;
using DotNetCloud.Core.Data;
using DotNetCloud.Core.ServiceDefaults;

var builder = WebApplicationBuilder.CreateBuilder(args);

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
    await dbInitializer.EnsureDatabaseAsync();
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
