using Ailos.Common.Configuration;
using Ailos.Common.Infrastructure.Data;
using Ailos.Common.Infrastructure.Implementations.Idempotencia;
using Ailos.Common.Infrastructure.Security;
using Ailos.Common.Infrastructure.Security.Extensions;
using Ailos.Common.Messaging.Implementations;
using Ailos.Common.Presentation.Filters;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ailos.Common.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAilosCommon(
        this IServiceCollection services,
        IConfiguration configuration,
        string databaseConnectionString = null!)
    {
        LoadEnvironmentConfiguration();

        var jwtSettings = new JwtSettings();
        configuration.GetSection(JwtSettings.SectionName).Bind(jwtSettings);

        if (string.IsNullOrEmpty(jwtSettings.Secret))
        {
            jwtSettings.Secret = Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? throw new InvalidOperationException("JWT_SECRET não configurado");
        }

        if (string.IsNullOrEmpty(jwtSettings.Issuer))
        {
            jwtSettings.Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
                ?? "AilosBankingSystem";
        }

        if (string.IsNullOrEmpty(jwtSettings.Audience))
        {
            jwtSettings.Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
                ?? "AilosClients";
        }

        if (string.IsNullOrEmpty(jwtSettings.Secret))
            throw new InvalidOperationException("JWT_SECRET não configurado");

        services.AddSingleton(jwtSettings);
        services.Configure<JwtSettings>(options =>
        {
            options.Secret = jwtSettings.Secret;
            options.Issuer = jwtSettings.Issuer;
            options.Audience = jwtSettings.Audience;
            options.ExpirationMinutes = jwtSettings.ExpirationMinutes;
        });

        services.AddJwtAuthentication(jwtSettings);

        services.AddSingleton<IJwtTokenService>(sp =>
        {
            var settings = sp.GetRequiredService<JwtSettings>();
            return new JwtTokenService(Options.Create(settings));
        });

        if (!string.IsNullOrEmpty(databaseConnectionString))
        {
            services.AddSingleton<IDbConnectionFactory>(
                new SqliteConnectionFactory(databaseConnectionString));
        }

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        services.AddMemoryCache();
        services.AddSingleton<IIdempotenciaService, IdempotenciaService>();

        services.AddControllers(options =>
        {
            options.Filters.Add<ApiExceptionFilter>();
        });

        return services;
    }

    public static IServiceCollection AddAilosCommonWithServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAilosCommon(configuration);

        return services;
    }

    private static void LoadEnvironmentConfiguration()
    {
        var rootEnvPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");

        if (File.Exists(rootEnvPath))
        {
            Env.Load(rootEnvPath);
        }
        else
        {
            Env.Load();
        }
    }

    public static IServiceCollection AddAilosKafka(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KafkaSettings>(
            configuration.GetSection(KafkaSettings.SectionName));

        services.PostConfigure<KafkaSettings>(settings =>
        {
            var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
            var transferenciasTopic = Environment.GetEnvironmentVariable("KAFKA_TRANSFERENCIAS_TOPIC");
            var tarifasTopic = Environment.GetEnvironmentVariable("KAFKA_TARIFAS_TOPIC");
            var consumerGroup = Environment.GetEnvironmentVariable("KAFKA_CONSUMER_GROUP");

            if (!string.IsNullOrEmpty(bootstrapServers))
                settings.BootstrapServers = bootstrapServers;

            if (!string.IsNullOrEmpty(transferenciasTopic))
                settings.TransferenciasTopic = transferenciasTopic;

            if (!string.IsNullOrEmpty(tarifasTopic))
                settings.TarifasTopic = tarifasTopic;

            if (!string.IsNullOrEmpty(consumerGroup))
                settings.ConsumerGroup = consumerGroup;
        });

        services.AddSingleton<KafkaConnectionFactory>();
        services.AddSingleton<IKafkaProducerService, KafkaProducerService>();

        return services;
    }
}
