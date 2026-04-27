using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using pm.Application;
using pm.Application.Settings;
using pm.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var jwtSettingsSection = builder.Configuration.GetRequiredSection("JwtSettings");
var jwtSettings = jwtSettingsSection.Get<JwtSettings>()
                  ?? throw new InvalidOperationException("Missing JwtSettings configuration.");

ValidateJwtSettings(jwtSettings);

builder.Services.Configure<JwtSettings>(jwtSettingsSection);
builder.Services.AddInfrastructure();
builder.Services.AddApplication();

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
            InvalidOperationException   => (409, ex.Message),
            _ => (500, app.Environment.IsDevelopment()
                    ? $"{ex.GetType().Name}: {ex.Message}"
                    : "An unexpected error occurred.")
        };
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = message });
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
