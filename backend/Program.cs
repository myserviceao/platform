using Microsoft.EntityFrameworkCore;
using MyServiceAO.Data;
using MyServiceAO.Services;
using MyServiceAO.Services.ServiceTitan;

var builder = WebApplication.CreateBuilder(args);

// ââ Database ââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââ
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;

if (!string.IsNullOrEmpty(databaseUrl))
{
    // Railway provides DATABASE_URL in postgres:// format â convert to Npgsql format
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("No database connection string found.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ââ Auth / Session ââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââ
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// ââ Services ââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââ
builder.Services.AddScoped<AuthService>();
builder.Services.AddHttpClient<ServiceTitanClient>();
builder.Services.AddScoped<ServiceTitanOAuthService>();
builder.Services.AddScoped<ServiceTitanSyncService>();
builder.Services.AddScoped<OutreachService>();
builder.Services.AddHostedService<SyncSchedulerService>();

// ââ Controllers âââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââââ
builder.Services.AddControllers();

// ââ CORS (dev only) âââââââââââââââââââââââââââââââââââââââââââââââââââââââââââ
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ââ Startup migrations ââââââââââââââââââââââââââââââââââââââââââââââââââââââââ
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DbMigrations.RunAsync(db);
}

// ââ Middleware pipeline âââââââââââââââââââââââââââââââââââââââââââââââââââââââ
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevCors");
}

app.UseSession();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

// ââ SPA fallback â serve index.html for all non-API routes âââââââââââââââââââ
app.MapFallbackToFile("index.html");

app.Run();
// cache-bust: 1773239314213

// Build trigger: 20260311190228

// Force rebuild 20260311211251
