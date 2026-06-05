using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using sum.Data;
using sum.Models;

var cultureInfo = new CultureInfo("cs-CZ");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

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

            // Create Homeworks table if it does not exist
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Homeworks (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        Description TEXT NULL,
                        Subject TEXT NOT NULL,
                        Deadline TEXT NOT NULL,
                        TeacherId INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        FOREIGN KEY (TeacherId) REFERENCES Users (Id) ON DELETE CASCADE
                    );";
                await cmd.ExecuteNonQueryAsync();
            }

            // Alter Messages to add soft-delete columns
            try
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Messages ADD COLUMN SenderDeleted INTEGER NOT NULL DEFAULT 0;";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch { }
            try
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Messages ADD COLUMN SenderDeletedAt TEXT NULL;";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch { }
            try
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Messages ADD COLUMN RecipientDeleted INTEGER NOT NULL DEFAULT 0;";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch { }
            try
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Messages ADD COLUMN RecipientDeletedAt TEXT NULL;";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch { }

            // Create HomeworkCompletions table if it does not exist
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS HomeworkCompletions (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        StudentId INTEGER NOT NULL,
                        HomeworkId INTEGER NOT NULL,
                        CompletedAt TEXT NOT NULL,
                        FOREIGN KEY (StudentId) REFERENCES Users (Id) ON DELETE CASCADE,
                        FOREIGN KEY (HomeworkId) REFERENCES Homeworks (Id) ON DELETE CASCADE,
                        UNIQUE(StudentId, HomeworkId)
                    );";
                await cmd.ExecuteNonQueryAsync();
            }

            // Alter Users to add E2E columns
            try
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Users ADD COLUMN PublicKey TEXT NULL;";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch { }
            try
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Users ADD COLUMN EncryptedPrivateKey TEXT NULL;";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch { }

            // Create ChatMessages table if it does not exist
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ChatMessages (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SenderId INTEGER NOT NULL,
                        RecipientId INTEGER NOT NULL,
                        EncryptedPayload TEXT NOT NULL,
                        SentAt TEXT NOT NULL,
                        FOREIGN KEY (SenderId) REFERENCES Users (Id) ON DELETE RESTRICT,
                        FOREIGN KEY (RecipientId) REFERENCES Users (Id) ON DELETE RESTRICT
                    );";
                await cmd.ExecuteNonQueryAsync();
            }

            // Create indexes for ChatMessages
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_ChatMessages_SenderId ON ChatMessages (SenderId);";
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_ChatMessages_RecipientId ON ChatMessages (RecipientId);";
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_ChatMessages_SentAt ON ChatMessages (SentAt);";
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }

            if (!wasOpen) await conn.CloseAsync();
        }
        else if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"PublicKey\" text NULL;");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"EncryptedPrivateKey\" text NULL;");

            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"AttachmentData\" bytea;");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"SecureId\" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';");

            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"SenderDeleted\" BOOLEAN NOT NULL DEFAULT FALSE;");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"SenderDeletedAt\" timestamp with time zone NULL;");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"RecipientDeleted\" BOOLEAN NOT NULL DEFAULT FALSE;");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"RecipientDeletedAt\" timestamp with time zone NULL;");

            // Create Homeworks table if it does not exist
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""Homeworks"" (
                    ""Id"" INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""Title"" TEXT NOT NULL,
                    ""Description"" TEXT NULL,
                    ""Subject"" TEXT NOT NULL,
                    ""Deadline"" timestamp with time zone NOT NULL,
                    ""TeacherId"" INTEGER NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""FK_Homeworks_Users_TeacherId"" FOREIGN KEY (""TeacherId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                );");

            // Create HomeworkCompletions table if it does not exist
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""HomeworkCompletions"" (
                    ""Id"" INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""StudentId"" INTEGER NOT NULL,
                    ""HomeworkId"" INTEGER NOT NULL,
                    ""CompletedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""FK_HomeworkCompletions_Users_StudentId"" FOREIGN KEY (""StudentId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_HomeworkCompletions_Homeworks_HomeworkId"" FOREIGN KEY (""HomeworkId"") REFERENCES ""Homeworks"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""UQ_HomeworkCompletions_Student_Homework"" UNIQUE (""StudentId"", ""HomeworkId"")
                );");

            // Create ChatMessages table and indexes if not exists (PostgreSQL)
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""ChatMessages"" (
                    ""Id"" INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""SenderId"" INTEGER NOT NULL,
                    ""RecipientId"" INTEGER NOT NULL,
                    ""EncryptedPayload"" TEXT NOT NULL,
                    ""SentAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""FK_ChatMessages_Users_SenderId"" FOREIGN KEY (""SenderId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT,
                    CONSTRAINT ""FK_ChatMessages_Users_RecipientId"" FOREIGN KEY (""RecipientId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
                );");

            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_ChatMessages_SenderId\" ON \"ChatMessages\" (\"SenderId\");");
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_ChatMessages_RecipientId\" ON \"ChatMessages\" (\"RecipientId\");");
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_ChatMessages_SentAt\" ON \"ChatMessages\" (\"SentAt\");");
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

        // Seed Teacher Account
        var teacherEmail = "ucitel@zschvalk.cz";
        var teacherExists = await db.Users.AnyAsync(u => u.Email == teacherEmail);
        if (!teacherExists)
        {
            string hashedPassword;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes("Ucitel123!"));
                hashedPassword = Convert.ToBase64String(hashBytes);
            }

            var teacher = new User
            {
                Username = "ucitel",
                Email = teacherEmail,
                FullName = "Mgr. Jan Novák",
                PasswordHash = hashedPassword,
                Role = "Teacher",
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(teacher);
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

var supportedCultures = new[] { new CultureInfo("cs-CZ") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("cs-CZ"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

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
