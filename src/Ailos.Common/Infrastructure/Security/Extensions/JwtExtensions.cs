using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Ailos.Common.Configuration;

namespace Ailos.Common.Infrastructure.Security.Extensions;

public static class JwtExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        JwtSettings jwtSettings)
    {
        services.AddSingleton(jwtSettings);
        
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false; // Para desenvolvimento
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ClockSkew = TimeSpan.Zero
            };
            
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine($"❌ Authentication failed: {context.Exception.Message}");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var contaId = context.Principal?.FindFirstValue("contaId");
                    Console.WriteLine($"✅ Token validated - UserId: {userId}, ContaId: {contaId}");
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Console.WriteLine($"⚠ JWT Challenge: {context.Error}");
                    return Task.CompletedTask;
                }
            };
        });
        
        return services;
    }
    
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        // PRIORIDADE: contaId (específico do sistema)
        // FALLBACK: NameIdentifier (padrão .NET)
        return principal.FindFirstValue("contaId")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User ID não encontrado no token");
    }
    
    public static long GetUserIdAsLong(this ClaimsPrincipal principal)
    {
        var userId = principal.GetUserId();
        if (!long.TryParse(userId, out var parsedId))
            throw new InvalidOperationException("User ID inválido no token");
        
        return parsedId;
    }
    
    public static string GetUserEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? throw new InvalidOperationException("Email não encontrado no token");
    }
}
