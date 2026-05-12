using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using pm.API.Services;
using pm.Application;
using pm.Application.Exceptions;
using pm.Application.Settings;
using pm.Infrastructure;

var envFile = FindEnvFile();
if (!string.IsNullOrWhiteSpace(envFile))
{
    foreach (var raw in File.ReadAllLines(envFile))
    {
        var line = raw.Trim();
        if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
        var idx = line.IndexOf('=');
        if (idx <= 0) continue;
        var key = line.Substring(0, idx).Trim();
        var val = line.Substring(idx + 1).Trim();
        if (string.IsNullOrEmpty(key)) continue;
        switch (key)
        {
            case "STRIPE_SECRET_KEY":
                Environment.SetEnvironmentVariable("Stripe__ApiKey", val);
                break;
            case "STRIPE_WEBHOOK_SECRET":
                Environment.SetEnvironmentVariable("Stripe__WebhookSecret", val);
                break;
            case "STRIPE_PUBLISHABLE_KEY":
                Environment.SetEnvironmentVariable("Stripe__PublishableKey", val);
                break;
            default:
                Environment.SetEnvironmentVariable(key, val);
                break;
        }
    }
}

var builder = WebApplication.CreateBuilder(args);
var jwtSettingsSection = builder.Configuration.GetRequiredSection("JwtSettings");
var jwtSettings = jwtSettingsSection.Get<JwtSettings>()
                  ?? throw new InvalidOperationException("Missing JwtSettings configuration.");

ValidateJwtSettings(jwtSettings);

builder.Services.Configure<JwtSettings>(jwtSettingsSection);
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.Configure<InvoicePdfSettings>(builder.Configuration.GetSection("InvoicePdf"));
builder.Services.Configure<ReminderSettings>(builder.Configuration.GetSection("Reminders"));
builder.Services.AddInfrastructure();
builder.Services.AddApplication();
builder.Services.AddHostedService<OverdueInvoiceReminderHostedService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token. Example: eyJhbGci..."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

var app = builder.Build();

await app.Services.GetRequiredService<DatabaseMigrator>().MigrateAsync();
await app.Services.GetRequiredService<DemoDataSeeder>().SeedAsync();

app.UseExceptionHandler(appBuilder =>
{
    appBuilder.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (status, message) = ex switch
        {
            UnauthorizedAccessException => (401, ex.Message),
            KeyNotFoundException        => (404, ex.Message),
            ApiValidationException      => (400, ex.Message),
            ApiConflictException        => (409, ex.Message),
            InvalidOperationException   => (409, ex.Message),
            null => (500, "An unexpected error occurred."),
            _ => (500, app.Environment.IsDevelopment()
                    ? $"{ex.GetType().Name}: {ex.Message}"
                    : "An unexpected error occurred.")
        };
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var response = ex is ApiValidationException validation
            ? new { error = message, errors = validation.Errors }
            : (object)new { error = message };
        await context.Response.WriteAsJsonAsync(response);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

static void ValidateJwtSettings(JwtSettings settings)
{
    if (string.IsNullOrWhiteSpace(settings.Secret))
    {
        throw new InvalidOperationException(
            "JwtSettings:Secret is required. Configure it in appsettings or via the JwtSettings__Secret environment variable.");
    }

    if (string.IsNullOrWhiteSpace(settings.Issuer))
    {
        throw new InvalidOperationException("JwtSettings:Issuer is required.");
    }

    if (string.IsNullOrWhiteSpace(settings.Audience))
    {
        throw new InvalidOperationException("JwtSettings:Audience is required.");
    }
}

static string? FindEnvFile()
{
    var locations = new[]
    {
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory
    };

    foreach (var start in locations)
    {
        var current = new DirectoryInfo(start);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }
    }

    return null;
}
