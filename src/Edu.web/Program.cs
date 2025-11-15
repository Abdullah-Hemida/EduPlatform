using Edu.Application.IServices;
using Edu.Domain.Entities;
using Edu.Infrastructure.Data;
using Edu.Infrastructure.Helpers;
using Edu.Infrastructure.Localization;
using Edu.Infrastructure.Persistence;
using Edu.Infrastructure.Services;
using Edu.Web.Views.Shared.Components.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// DB + Identity (your existing)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Localization: JSON string localizer registered below
builder.Services.AddLocalization(); // you can pass options => options.ResourcesPath = "Resources" if needed
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
// only register a generic if you implemented it; keep your existing registration if needed
// builder.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
builder.Services.AddScoped<IHeroService, HeroService>();
builder.Services.AddHttpContextAccessor();

// register Local by default
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

// switch by config
var provider = builder.Configuration["Storage:Provider"] ?? (builder.Environment.IsProduction() ? "Azure" : "Local");
if (provider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
{
    // AzureBlobStorageService has ctor(IConfiguration, ILogger<AzureBlobStorageService>)
    builder.Services.AddSingleton<AzureBlobOptions>(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        var opt = new AzureBlobOptions();
        cfg.GetSection("Storage:Azure").Bind(opt);
        return opt;
    });

    builder.Services.AddSingleton<IFileStorageService, AzureBlobStorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
}

// MVC + Razor Pages (Identity uses Razor Pages)
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.AddRazorPages();

builder.Services.AddAuthentication();

// Request localization (supported cultures)
// Keep cookie provider first so user selection wins; accept-language is a fallback
var supportedCultures = new[] { "en", "ar", "it" };
builder.Services.Configure<RequestLocalizationOptions>(opts =>
{
    var cultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
    opts.DefaultRequestCulture = new RequestCulture("en");
    opts.SupportedCultures = cultures;
    opts.SupportedUICultures = cultures;

    opts.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new CookieRequestCultureProvider(),               // cookie first (user preference)
        new AcceptLanguageHeaderRequestCultureProvider()  // then browser header
    };
});

// after builder created and before building the app
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

// Dev vs Prod wiring for IEmailSender
if (builder.Environment.IsDevelopment())
{
    // Use NoOp in development
    builder.Services.AddSingleton<IEmailSender, NoOpEmailSender>();
}
else
{
    // Production: real SMTP sender (already implemented)
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}
// Register adapter so Microsoft.Identity UI can resolve IEmailSender
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, IdentityEmailSenderAdapter>();

builder.Services.AddMemoryCache();

builder.Services.Configure<ReactiveCourseOptions>(builder.Configuration.GetSection("ReactiveCourse"));


var app = builder.Build();

// IMPORTANT: Run localization **before** routing/controllers so cookies are applied immediately
var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages(); // identity scaffolded pages

// Seed roles & admin (your existing snippet)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var config = services.GetRequiredService<IConfiguration>();
    await DbInitializer.InitializeAsync(services, config);
}

app.Run();





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