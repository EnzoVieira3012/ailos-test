using Ailos.Common.Configuration;
using Ailos.Common.Domain.Exceptions;
using Ailos.Common.Infrastructure.Data;
using Ailos.Common.Infrastructure.Security;
using Ailos.Common.Infrastructure.Security.Extensions;
using Ailos.Common.Messaging;
using Ailos.Common.Presentation.Filters;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ailos.Common.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAilosCommon(
    this IServiceCollection services,
    IConfiguration configuration,
    string databaseConnectionString = null!)
    {
        // 1. Carregar configurações do .env
        LoadEnvironmentConfiguration();

        // 2. Configurar JWT
        var jwtSettings = new JwtSettings();
        configuration.GetSection(JwtSettings.SectionName).Bind(jwtSettings);

        // Se não configurado no appsettings, tenta do .env
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

        services.AddJwtAuthentication(jwtSettings);

        // 3. Configurar banco de dados
        if (!string.IsNullOrEmpty(databaseConnectionString))
        {
            services.AddSingleton<IDbConnectionFactory>(
                new SqliteConnectionFactory(databaseConnectionString));
        }

        // 4. Configurar password hasher
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // 5. Configurar filtro global de exceções
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
        // Adiciona tudo do AddAilosCommon
        services.AddAilosCommon(configuration);

        // Adiciona configurações adicionais específicas
        return services;
    }

    private static void LoadEnvironmentConfiguration()
    {
        // Tenta carregar .env da raiz do projeto
        var rootEnvPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");

        if (File.Exists(rootEnvPath))
        {
            Env.Load(rootEnvPath);
        }
        else
        {
            // Tenta do diretório atual
            Env.Load();
        }

        // Log das configurações carregadas (apenas para debug)
        Console.WriteLine("=== Configurações Carregadas ===");
        Console.WriteLine($"ENCRYPTED_ID_SECRET: {Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET")?.Substring(0, 10)}...");
        Console.WriteLine($"JWT_ISSUER: {Environment.GetEnvironmentVariable("JWT_ISSUER")}");
        Console.WriteLine("===============================");
    }

    public static IServiceCollection AddAilosKafka(
    this IServiceCollection services,
    IConfiguration configuration)
    {
        // Bind padrão via Options
        services.Configure<KafkaSettings>(
            configuration.GetSection(KafkaSettings.SectionName));

        // Override via .env (se existir)
        services.PostConfigure<KafkaSettings>(settings =>
        {
            settings.BootstrapServers =
                Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS")
                ?? settings.BootstrapServers;

            settings.TransferenciasTopic =
                Environment.GetEnvironmentVariable("KAFKA_TRANSFERENCIAS_TOPIC")
                ?? settings.TransferenciasTopic;

            settings.TarifasTopic =
                Environment.GetEnvironmentVariable("KAFKA_TARIFAS_TOPIC")
                ?? settings.TarifasTopic;
        });

        // Infra Kafka
        services.AddSingleton<KafkaConnectionFactory>();
        services.AddSingleton<IKafkaProducerService, KafkaProducerService>();

        return services;
    }

}
