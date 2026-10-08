using Amazon.S3;
using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Localization;
using Edu.Infrastructure.Options;
using Edu.Infrastructure.Persistence;
using Edu.Infrastructure.Services;
using Edu.Web.Helpers;
using Edu.Web.Views.Shared.Components.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. DATABASE CONFIGURATION (PostgreSQL)
// ==========================================

var rawConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                          ?? Environment.GetEnvironmentVariable("database_url")
                          ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(rawConnectionString))
{
    throw new InvalidOperationException("The DATABASE_URL connection configuration string is completely missing.");
}

string connectionString;

// Safely translate the DigitalOcean URL format to classic .NET key-value pairs
if (rawConnectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
{
    var databaseUri = new Uri(rawConnectionString);
    var userInfo = databaseUri.UserInfo.Split(':');

    var username = userInfo.Length > 0 ? userInfo[0] : string.Empty;
    var password = userInfo.Length > 1 ? userInfo[1] : string.Empty;
    var databaseName = databaseUri.LocalPath.TrimStart('/');

    connectionString = $"Host={databaseUri.Host};Port={databaseUri.Port};Database={databaseName};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
}
else
{
    connectionString = rawConnectionString;
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            sqlOptions.CommandTimeout(180);
        }));

// ==========================================
// 2. IDENTITY CONFIGURATION
// ==========================================

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ==========================================
// 3. STORAGE PROVIDER CONFIGURATION (S3)
// ==========================================

builder.Services.Configure<DigitalOceanSpacesOptions>(builder.Configuration.GetSection("Storage:Spaces"));

var storageProvider = builder.Configuration["Storage:Provider"]
    ?? (builder.Environment.IsProduction() ? "Spaces" : "Local");

if (storageProvider.Equals("Spaces", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IAmazonS3>(sp =>
    {
        var opts = sp
            .GetRequiredService<IOptions<DigitalOceanSpacesOptions>>()
            .Value;

        var logger = sp.GetRequiredService<ILoggerFactory>()
                       .CreateLogger("DigitalOceanSpaces");


        if (string.IsNullOrWhiteSpace(opts.AccessKey) ||
            string.IsNullOrWhiteSpace(opts.SecretKey))
        {
            throw new InvalidOperationException(
                "DigitalOcean Spaces Key Credentials are missing.");
        }

        var config = new AmazonS3Config
        {
            ServiceURL = opts.ServiceUrl
        };

        return new AmazonS3Client(
            opts.AccessKey,
            opts.SecretKey,
            config);
    });
    builder.Services.AddScoped<IFileStorageService, DigitalOceanSpacesStorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
}

// ==========================================
// 4. CORE SERVICES & OPTIONS MAPPINGS
// ==========================================

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<AdminSeedOptions>(builder.Configuration.GetSection("AdminUser"));
builder.Services.Configure<ReactiveCourseOptions>(builder.Configuration.GetSection("ReactiveCourse"));

builder.Services.AddLocalization();
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<IHeroService, HeroService>();
builder.Services.AddScoped<IEmailService, MailKitEmailService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserCultureProvider, UserCultureProvider>();

// ==========================================
// 5. AUTHENTICATION & AUTHORIZATION
// ==========================================

builder.Services.AddAuthentication();
builder.Services.AddScoped<IAuthorizationHandler, TeacherApprovedHandler>();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("TeacherApproved", policy =>
    {
        policy.RequireRole("Teacher");
        policy.AddRequirements(new TeacherApprovedRequirement());
    });

// ==========================================
// 6. CONTROLLERS, RAZOR PAGES & LOCALIZATION
// ==========================================

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.AddRazorPages();

var supportedCultures = new[] { "en", "ar", "it" };
builder.Services.Configure<RequestLocalizationOptions>(opts =>
{
    var cultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
    opts.DefaultRequestCulture = new RequestCulture("it");
    opts.SupportedCultures = cultures;
    opts.SupportedUICultures = cultures;

    opts.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

// Dynamically bind to the port DigitalOcean assigns to the container
builder.WebHost.UseUrls($"http://*:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

var app = builder.Build();

// ==========================================
// 7. REQUEST PIPELINE (MIDDLEWARE)
// ==========================================

var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(exceptionApp =>
    {
        exceptionApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain";

            await context.Response.WriteAsync(
                "An unexpected error occurred. Check the application logs.");
        });
    });

    app.UseHsts();
}

// 🟢 FIX: Do not redirect to HTTPS if we are inside DigitalOcean's internal health-checking environment
if (!rawConnectionString.Contains("digitalocean", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ==========================================
// 8. ROUTING & DATABASE SEEDING
// ==========================================

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var config = services.GetRequiredService<IConfiguration>();
    await DbInitializer.InitializeAsync(services, config);
}

app.Run();




//using Edu.Application.IServices;
//using Edu.Domain.Entities;
//using Edu.Infrastructure.Data;
//using Edu.Infrastructure.Helpers;
//using Edu.Infrastructure.Localization;
//using Edu.Infrastructure.Persistence;
//using Edu.Infrastructure.Services;
//using Edu.Web.Helpers;
//using Edu.Web.Views.Shared.Components.Services;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Localization;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Localization;
//using Microsoft.Extensions.Options;
//using System.Globalization;
//using Microsoft.AspNetCore.Authorization;

//var builder = WebApplication.CreateBuilder(args);

//// DB + Identity (your existing)
//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseSqlServer(
//        connectionString,
//        sqlOptions =>
//        {
//            // Retry on transient failures: up to 5 retries, 30s max delay
//            sqlOptions.EnableRetryOnFailure(
//                maxRetryCount: 5,
//                maxRetryDelay: TimeSpan.FromSeconds(30),
//                errorNumbersToAdd: null);

//            // Increase command timeout (seconds) so large migration scripts don't timeout
//            sqlOptions.CommandTimeout(180); // 3 minutes — increase if needed
//        })
//);

//builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
//{
//    options.Password.RequireDigit = true;
//    options.Password.RequiredLength = 6;
//    options.User.RequireUniqueEmail = true;
//    options.SignIn.RequireConfirmedAccount = false;
//})
//.AddEntityFrameworkStores<ApplicationDbContext>()
//.AddDefaultTokenProviders();

//// Localization: JSON string localizer registered below
//builder.Services.AddLocalization(); // you can pass options => options.ResourcesPath = "Resources" if needed
//builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
//// only register a generic if you implemented it; keep your existing registration if needed
//// builder.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
//builder.Services.AddScoped<IHeroService, HeroService>();
//builder.Services.AddHttpContextAccessor();

//// register Local by default
//builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

//// switch by config
//var provider = builder.Configuration["Storage:Provider"] ?? (builder.Environment.IsProduction() ? "Azure" : "Local");
//if (provider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
//{
//    // AzureBlobStorageService has ctor(IConfiguration, ILogger<AzureBlobStorageService>)
//    builder.Services.AddSingleton<AzureBlobOptions>(sp =>
//    {
//        var cfg = sp.GetRequiredService<IConfiguration>();
//        var opt = new AzureBlobOptions();
//        cfg.GetSection("Storage:Azure").Bind(opt);
//        return opt;
//    });

//    builder.Services.AddSingleton<IFileStorageService, AzureBlobStorageService>();
//}
//else
//{
//    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
//}

//// MVC + Razor Pages (Identity uses Razor Pages)
//builder.Services.AddControllersWithViews()
//    .AddViewLocalization()
//    .AddDataAnnotationsLocalization();

//builder.Services.AddRazorPages();

//builder.Services.AddAuthentication();

//builder.Services.AddScoped<IAuthorizationHandler, TeacherApprovedHandler>();

//builder.Services.AddAuthorizationBuilder()
//    .AddPolicy("TeacherApproved", policy =>
//    {
//        policy.RequireRole("Teacher"); // optional but recommended
//        policy.AddRequirements(new TeacherApprovedRequirement());
//    });


//// Request localization (supported cultures)
//// Keep cookie provider first so user selection wins; accept-language is a fallback
//var supportedCultures = new[] { "en", "ar", "it" };
//builder.Services.Configure<RequestLocalizationOptions>(opts =>
//{
//    var cultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
//    opts.DefaultRequestCulture = new RequestCulture("it");
//    opts.SupportedCultures = cultures;
//    opts.SupportedUICultures = cultures;

//    opts.RequestCultureProviders = new IRequestCultureProvider[]
//    {
//        new CookieRequestCultureProvider(),               // cookie first (user preference)
//        new AcceptLanguageHeaderRequestCultureProvider()  // then browser header
//    };
//});

//// after builder created and before building the app
//builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

//// Dev vs Prod wiring for IEmailSender
//if (builder.Environment.IsDevelopment())
//{
//    // Use NoOp in development
//    builder.Services.AddSingleton<IEmailSender, DevEmailSender>();
//}
//else
//{
//    // Production: real SMTP sender (already implemented)
//    builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();
//}

//builder.Services.AddScoped<IUserCultureProvider, UserCultureProvider>();
//builder.Services.AddScoped<INotificationService, NotificationService>();

//builder.Services.AddMemoryCache();

//builder.Services.Configure<ReactiveCourseOptions>(builder.Configuration.GetSection("ReactiveCourse"));


//var app = builder.Build();

//// IMPORTANT: Run localization **before** routing/controllers so cookies are applied immediately
//var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
//app.UseRequestLocalization(locOptions.Value);

//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//    app.UseHsts();
//}
//app.UseHttpsRedirection();
//app.UseStaticFiles();

//app.UseRouting();
//app.UseAuthentication();
//app.UseAuthorization();

//app.MapControllerRoute(
//    name: "areas",
//    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

//app.MapRazorPages(); // identity scaffolded pages

//// Seed roles & admin (your existing snippet)
//using (var scope = app.Services.CreateScope())
//{
//    var services = scope.ServiceProvider;
//    var config = services.GetRequiredService<IConfiguration>();
//    await DbInitializer.InitializeAsync(services, config);
//}

//app.Run();





//builder.Services.ConfigureApplicationCookie(options =>
//{
//    options.LoginPath = "/Identity/Account/Login";
//    options.LogoutPath = "/Identity/Account/Logout";
//    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
//    options.Cookie.SameSite = SameSiteMode.Lax; // allows normal form-post flows
//    options.Cookie.HttpOnly = true;
//    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // use Always in prod
//    options.SlidingExpiration = true;
//    options.ExpireTimeSpan = TimeSpan.FromDays(14);
//});

// DB + Identity
//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseSqlServer(
//        connectionString,
//        sqlOptions =>
//        {
//            sqlOptions.EnableRetryOnFailure(
//                maxRetryCount: 5,
//                maxRetryDelay: TimeSpan.FromSeconds(30),
//                errorNumbersToAdd: null);

//            sqlOptions.CommandTimeout(180);
//        }));
// DB + Identity


//var storageProvider = builder.Configuration["Storage:Provider"]
//    ?? (builder.Environment.IsProduction() ? "Azure" : "Local");

//if (storageProvider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
//{
//    builder.Services.AddSingleton(sp =>
//    {
//        var opts = sp.GetRequiredService<IOptions<AzureBlobOptions>>().Value;

//        if (!string.IsNullOrWhiteSpace(opts.ConnectionString))
//        {
//            return new BlobServiceClient(opts.ConnectionString);
//        }

//        throw new InvalidOperationException("Storage:Azure:ConnectionString is missing.");
//        // If you later switch to managed identity, replace this with:
//        // return new BlobServiceClient(new Uri(opts.AccountUrl), new DefaultAzureCredential());
//    });

//    builder.Services.AddSingleton<IFileStorageService, AzureBlobStorageService>();
//}
//else
//{
//    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
//}