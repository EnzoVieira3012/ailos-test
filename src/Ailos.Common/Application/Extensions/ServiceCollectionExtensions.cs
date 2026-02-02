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
using Microsoft.Extensions.Options;

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
            var secret = Environment.GetEnvironmentVariable("JWT_SECRET");
            if (string.IsNullOrEmpty(secret))
                throw new InvalidOperationException("JWT_SECRET não configurado");
            jwtSettings.Secret = secret;
        }

        if (string.IsNullOrEmpty(jwtSettings.Issuer))
        {
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
            jwtSettings.Issuer = issuer ?? "AilosBankingSystem";
        }

        if (string.IsNullOrEmpty(jwtSettings.Audience))
        {
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
            jwtSettings.Audience = audience ?? "AilosClients";
        }

        // Validar configuração
        if (string.IsNullOrEmpty(jwtSettings.Secret))
            throw new InvalidOperationException("JWT_SECRET não configurado");

        // Registrar JwtSettings como IOptions
        services.Configure<JwtSettings>(options =>
        {
            options.Secret = jwtSettings.Secret;
            options.Issuer = jwtSettings.Issuer;
            options.Audience = jwtSettings.Audience;
            options.ExpirationMinutes = jwtSettings.ExpirationMinutes;
        });

        // Configurar autenticação JWT
        services.AddJwtAuthentication(jwtSettings);

        // Registrar IJwtTokenService (corrigido para usar IOptions)
        services.AddSingleton<IJwtTokenService>(sp =>
        {
            var options = Options.Create(jwtSettings);
            return new JwtTokenService(options);
        });

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
        var encryptedIdSecret = Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET");
        var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
        
        Console.WriteLine("=== Configurações Carregadas ===");
        Console.WriteLine($"ENCRYPTED_ID_SECRET: {(encryptedIdSecret?.Length > 10 ? encryptedIdSecret.Substring(0, 10) + "..." : "Não configurado")}");
        Console.WriteLine($"JWT_ISSUER: {jwtIssuer ?? "Não configurado"}");
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
            var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS");
            var transferenciasTopic = Environment.GetEnvironmentVariable("KAFKA_TRANSFERENCIAS_TOPIC");
            var tarifasTopic = Environment.GetEnvironmentVariable("KAFKA_TARIFAS_TOPIC");

            if (!string.IsNullOrEmpty(bootstrapServers))
                settings.BootstrapServers = bootstrapServers;

            if (!string.IsNullOrEmpty(transferenciasTopic))
                settings.TransferenciasTopic = transferenciasTopic;

            if (!string.IsNullOrEmpty(tarifasTopic))
                settings.TarifasTopic = tarifasTopic;
        });

        // Infra Kafka
        services.AddSingleton<KafkaConnectionFactory>();
        services.AddSingleton<IKafkaProducerService, KafkaProducerService>();

        return services;
    }
}