using System.Text;
using System.Text.Json.Serialization;
using GLMS.Api.Middleware;
using GLMS.Api.Services;
using GLMS.Web.Data;
using GLMS.Web.Models;
using GLMS.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) && builder.Environment.IsDevelopment())
{
    signingKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Jwt:SigningKey"] = signingKey
    });
}

var issuer = builder.Configuration["Jwt:Issuer"] ?? "GLMS.Api";
var audience = builder.Configuration["Jwt:Audience"] ?? "GLMS.Web";
if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
{
    throw new InvalidOperationException("Set Jwt:SigningKey to a secret of at least 32 bytes.");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Configure the ConnectionStrings:DefaultConnection setting for the API.");
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddDbContext<GlmsDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(Program).Assembly.FullName)));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<GlmsDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
builder.Services.Configure<SmtpEmailOptions>(builder.Configuration.GetSection("SmtpEmail"));
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IAccountEmailSender, DevelopmentAccountEmailSender>();
}
else
{
    builder.Services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();
}
builder.Services.AddScoped<EmailVerificationService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IGlmsDataService, GlmsDataService>();
builder.Services.AddScoped<AgreementFileStore>();
builder.Services.AddHttpClient<IExchangeRateProvider, ExchangeRateApiProvider>(client =>
{
    client.BaseAddress = new Uri("https://open.er-api.com/v6/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<GlmsDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program;
