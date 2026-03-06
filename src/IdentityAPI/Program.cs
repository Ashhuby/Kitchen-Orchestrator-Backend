using KitchenOrchestrator.IdentityAPI.Configuration;
using KitchenOrchestrator.IdentityAPI.Data;
using KitchenOrchestrator.IdentityAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configuration Binding
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<SteamOptions>(builder.Configuration.GetSection("Steam"));
builder.Services.Configure<ServerAuthOptions>(builder.Configuration.GetSection("ServerAuth"));

// Database Registration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Service Registration
builder.Services.AddHttpClient<ISteamAuthService, SteamAuthService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();

// API Infrastructure
builder.Services.AddControllers();

// Security & JWT Validation
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// 6. Request Pipeline
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();

// Health Check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();