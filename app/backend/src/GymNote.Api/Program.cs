using System.Text;
using GymNote.Api.Auth;
using GymNote.Api.Data;
using GymNote.Api.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Container-friendly logging: single-line entries to stdout.
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.UseUtcTimestamp = true;
    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

builder.Services.AddProblemDetails();
// Stateless: keep the (unused) data protection keys in memory instead of the container filesystem.
builder.Services.AddDataProtection();
builder.Services.Configure<KeyManagementOptions>(o =>
{
    o.XmlRepository = new InMemoryXmlRepository();
    o.XmlEncryptor = new NullXmlEncryptor();
});
builder.Services.AddSingleton(TimeProvider.System);

// Configuration is resolved lazily from DI so test hosts and environment variables can override it.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings__Default is not configured.");
    }
    options.UseNpgsql(connectionString);
});

builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration(JwtSettings.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, jwt) =>
    {
        var settings = jwt.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();

var app = builder.Build();

// Fail fast on missing/invalid JWT configuration before touching the database.
_ = app.Services.GetRequiredService<IOptions<JwtSettings>>().Value;

await DatabaseInitializer.InitializeAsync(app);

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

// Liveness/readiness for ALB and Kubernetes probes: deliberately does not touch the database.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapExerciseEndpoints();

app.Run();

public partial class Program;
