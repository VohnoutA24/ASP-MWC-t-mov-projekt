using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using sum.Data;

var builder = WebApplication.CreateBuilder(args);

// Initialize EncryptionHelper
sum.Services.EncryptionHelper.Initialize(builder.Configuration["Encryption:Key"]);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure file upload limits (25 MB attachments)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 30 * 1024 * 1024; // 30 MB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // 30 MB
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "5089";
builder.WebHost.UseUrls($"http://*:{port}");

// Configure database (SQLite local, PostgreSQL in cloud/Render)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        options.UseNpgsql(ParseDatabaseUrl(databaseUrl));
    }
    else if (!string.IsNullOrEmpty(connectionString) && (connectionString.Contains("Host=") || connectionString.Contains("Server=")))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString ?? "Data Source=school.db");
    }
});

// Configure cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Auto-migrate database columns if they don't exist (ensures older databases get updated automatically)
    try
    {
        if (db.Database.IsSqlite())
        {
            var conn = db.Database.GetDbConnection();
            var wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) await conn.OpenAsync();

            var hasAttachmentData = false;
            var hasSecureId = false;

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(Messages);";
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var columnName = reader["name"]?.ToString();
                        if (columnName == "AttachmentData") hasAttachmentData = true;
                        if (columnName == "SecureId") hasSecureId = true;
                    }
                }
            }

            if (!hasAttachmentData)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Messages ADD COLUMN AttachmentData BLOB;";
                await alterCmd.ExecuteNonQueryAsync();
            }

            if (!hasSecureId)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Messages ADD COLUMN SecureId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';";
                await alterCmd.ExecuteNonQueryAsync();
            }

            if (!wasOpen) await conn.CloseAsync();
        }
        else if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"AttachmentData\" bytea;");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"SecureId\" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");
        }

        // Backfill SecureId for any legacy database rows
        var legacyMessages = await db.Messages.Where(m => m.SecureId == Guid.Empty).ToListAsync();
        if (legacyMessages.Any())
        {
            foreach (var msg in legacyMessages)
            {
                msg.SecureId = Guid.NewGuid();
            }
            await db.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error running database migrations on startup: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// Helper method to parse postgres:// connection string from Render
string ParseDatabaseUrl(string databaseUrl)
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var username = userInfo[0];
    var password = userInfo.Length > 1 ? userInfo[1] : "";
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.TrimStart('/');

    return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
}
