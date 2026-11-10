using System.Globalization;
using System.Text;
using foxtrot_store_backend.Resources;

using foxtrot_store_backend.Data;
using foxtrot_store_backend.Services;
using foxtrot_store_backend.Services.LocalStorage;
using foxtrot_store_backend.Services.Localization;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;


var builder = WebApplication.CreateBuilder(args);

// Localization
builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

var supportedCultures = new[]
{
    new CultureInfo("uk-UA"),
    new CultureInfo("en-US")
};

builder.Services.Configure<RequestLocalizationOptions>(
    options =>
    {
        options.DefaultRequestCulture =
            new RequestCulture("uk-UA");

        options.SupportedCultures = supportedCultures;
        options.SupportedUICultures = supportedCultures;

        options.FallBackToParentCultures = false;
        options.FallBackToParentUICultures = false;

        options.RequestCultureProviders =
        [
            new QueryStringRequestCultureProvider(),
            new UserClaimRequestCultureProvider(),
            new AcceptLanguageHeaderRequestCultureProvider()
        ];
    });

// Database
builder.Services.AddDbContext<DataContext>(options =>
    options.UseMySQL(
        builder.Configuration.GetConnectionString("DefaultConnection")!
    ));

// Services
builder.Services.AddScoped<DataAccessor>();
builder.Services.AddScoped<IdentityInitializer>();

builder.Services
    .AddControllersWithViews()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider =
            (type, factory) =>
                factory.Create(typeof(SharedResources));
    });

// Identity
builder.Services
    .AddIdentityCore<IdentityUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<DataContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<LocalizedIdentityErrorDescriber>();

// JWT
var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "JWT signing key is not configured.");
}

var jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKey);

if (jwtKeyBytes.Length < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must contain at least 32 bytes for HS256.");
}

var signingKey = new SymmetricSecurityKey(jwtKeyBytes);

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? "FoxtrotStore";

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? "FoxtrotStoreClient";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,

                ValidAlgorithms =
                [
                    SecurityAlgorithms.HmacSha256
                ],
                ClockSkew = TimeSpan.FromSeconds(30),

                NameClaimType = "unique_name",
                RoleClaimType = "role"
            };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;

                var userId = principal?
                    .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                var tokenSecurityStamp = principal?
                    .FindFirst("security_stamp")?.Value;

                if (string.IsNullOrWhiteSpace(userId) ||
                    string.IsNullOrWhiteSpace(tokenSecurityStamp))
                {
                    context.Fail("Invalid access token.");
                    return;
                }

                var userManager = context.HttpContext.RequestServices
                    .GetRequiredService<UserManager<IdentityUser>>();

                var user = await userManager.FindByIdAsync(userId);

                if (user is null)
                {
                    context.Fail("User no longer exists.");
                    return;
                }

                var currentSecurityStamp =
                    await userManager.GetSecurityStampAsync(user);

                if (!string.Equals(
                    currentSecurityStamp,
                    tokenSecurityStamp,
                    StringComparison.Ordinal))
                {
                    context.Fail("Access token has been revoked.");
                    return;
                }

                if (await userManager.IsLockedOutAsync(user))
                {
                    context.Fail("User account is locked out.");
                    return;
                }

                var tokenRoles = principal!
                    .FindAll("role")
                    .Select(claim => claim.Value)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var currentRoles = (await userManager.GetRolesAsync(user))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!tokenRoles.SetEquals(currentRoles))
                {
                    context.Fail("User roles have changed.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddLocalStorage(builder.Configuration);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Identity initialization
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider
        .GetRequiredService<IdentityInitializer>();

    await initializer.InitializeAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("Frontend");

// Authentication must run before localization,
// because the localization provider reads the "locale" claim.
app.UseAuthentication();

app.UseRequestLocalization();

app.UseAuthorization();

app.MapControllers();

app.Run();