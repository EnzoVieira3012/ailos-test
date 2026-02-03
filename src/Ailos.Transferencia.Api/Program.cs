using Ailos.Common.Application.Extensions;
using Ailos.Common.Application.Middleware;
using Ailos.Common.Infrastructure.Data;
using Ailos.Common.Presentation.Middleware;
using Ailos.EncryptedId;
using Ailos.EncryptedId.JsonConverters;
using Ailos.Transferencia.Api.Application.Services;
using Ailos.Transferencia.Api.Infrastructure.Clients.Implementations;
using Ailos.Transferencia.Api.Infrastructure.Clients.Interfaces;
using Ailos.Transferencia.Api.Infrastructure.Repositories;
using Ailos.Transferencia.Api.Infrastructure.Repositories.Implementations;
using DotNetEnv;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        "/app/logs/transferencia-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

try
{
    Env.Load();

    Environment.SetEnvironmentVariable("JWT_AUDIENCE", "AilosClients");
    Environment.SetEnvironmentVariable("JWT_ISSUER", "AilosBankingSystem");
    var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
    if (string.IsNullOrEmpty(jwtSecret))
    {
        throw new InvalidOperationException("JWT_SECRET não configurado");
    }

    var envVars = new
    {
        EncryptedIdLoaded = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET")),
        JwtSecretLoaded = !string.IsNullOrEmpty(jwtSecret),
        JwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER"),
        JwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
        KafkaServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS"),
        ContaApiUrl = Environment.GetEnvironmentVariable("CONTA_CORRENTE_API_URL")
    };

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    var dbConnection = "Data Source=/app/data/transferencia.db";

    builder.Configuration["Jwt:Audience"] = null;
    builder.Configuration["Jwt:Issuer"] = null;
    builder.Configuration["Jwt:Secret"] = null;

    builder.Services.AddAilosCommon(builder.Configuration, dbConnection);

    var tarifaConfig = new TarifaConfig
    {
        ValorTarifa = decimal.TryParse(Environment.GetEnvironmentVariable("TARIFA_VALOR"), out var tarifa)
            ? tarifa : 2.00m
    };
    builder.Services.AddSingleton(tarifaConfig);

    var encryptedIdSecret = Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET");
    if (string.IsNullOrEmpty(encryptedIdSecret))
    {
        throw new InvalidOperationException("ENCRYPTED_ID_SECRET não configurada");
    }

    try
    {
        var encryptedIdService = EncryptedIdFactory.CreateService(encryptedIdSecret);
        
        builder.Services.AddSingleton<IEncryptedIdService>(_ => encryptedIdService);
        
        var testService = EncryptedIdFactory.CreateService(encryptedIdSecret);
        var testId = 12345;
        var encrypted = testService.Encrypt(testId);
        var decrypted = testService.Decrypt(encrypted);
        
        if (testId != decrypted)
        {
            throw new InvalidOperationException("EncryptedIdService não está funcionando corretamente");
        }
    }
    catch (Exception)
    {
        throw;
    }

    builder.Services.AddAilosKafka(builder.Configuration);

    var contaCorrenteApiUrl = Environment.GetEnvironmentVariable("CONTA_CORRENTE_API_URL")
        ?? "http://conta-corrente-api:80";

    builder.Services.AddHttpClient<IContaCorrenteClient, ContaCorrenteClient>((provider, client) =>
    {
        client.BaseAddress = new Uri(contaCorrenteApiUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddScoped<ITransferenciaRepository, TransferenciaRepository>();
    builder.Services.AddScoped<IIdempotenciaRepository, IdempotenciaRepository>();

    builder.Services.AddScoped<ITransferenciaService, TransferenciaService>();
    builder.Services.AddScoped<IIdempotenciaService, IdempotenciaService>();

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            if (options.JsonSerializerOptions.Converters.All(c => c.GetType() != typeof(EncryptedIdJsonConverter)))
            {
                options.JsonSerializerOptions.Converters.Add(new EncryptedIdJsonConverter());
            }
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Ailos Transferência API",
            Version = "v1",
            Description = "API para transferências bancárias com Kafka e tarifação automática"
        });

        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization usando esquema Bearer. Exemplo: \"Bearer {token}\"",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddMemoryCache();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseRouting();

    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ExceptionMiddleware>();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ailos Transferência API v1");
        c.RoutePrefix = string.Empty;
        c.DisplayRequestDuration();
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapGet("/health", () => Results.Json(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        service = "transferencia-api",
        database = "connected",
        kafka = "configured",
        jwt_configured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_SECRET")),
        encrypted_id_configured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET"))
    }));

    app.MapGet("/healthz", () => "OK");

    await InitializeDatabase(app.Services);

    using var scope = app.Services.CreateScope();
    try
    {
        var jwtSettings = scope.ServiceProvider.GetService<Ailos.Common.Configuration.JwtSettings>();
        
        var encryptedIdService = scope.ServiceProvider.GetService<IEncryptedIdService>();
        if (encryptedIdService != null)
        {
            try
            {
                var testId = 999;
                var encrypted = encryptedIdService.Encrypt(testId);
                var decrypted = encryptedIdService.Decrypt(encrypted);
            }
            catch (Exception)
            {
            }
        }
    }
    catch (Exception)
    {
    }

    app.Run();
}
catch (Exception)
{
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static async Task InitializeDatabase(IServiceProvider services)
{
    try
    {
        using var scope = services.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        using var connection = connectionFactory.CreateConnection();
        connection.Open();

        var sql = @"
            -- Tabela principal de transferências
            CREATE TABLE IF NOT EXISTS transferencia (
                idtransferencia INTEGER PRIMARY KEY AUTOINCREMENT,
                idcontacorrente_origem INTEGER NOT NULL,
                idcontacorrente_destino INTEGER NOT NULL,
                datamovimento TEXT NOT NULL DEFAULT (datetime('now')),
                valor REAL NOT NULL,
                tarifa_aplicada REAL,
                status TEXT NOT NULL DEFAULT 'PROCESSANDO',
                mensagem_erro TEXT,
                identificacao_requisicao TEXT UNIQUE,
                CHECK (status IN ('PROCESSANDO', 'CONCLUIDA', 'FALHA', 'ESTORNADA'))
            );

            -- Tabela de idempotência (específica para transferência)
            CREATE TABLE IF NOT EXISTS idempotencia (
                chave_idempotencia TEXT PRIMARY KEY,
                requisicao TEXT,
                resultado TEXT,
                data_criacao TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- Índices
            CREATE INDEX IF NOT EXISTS idx_transferencia_origem ON transferencia(idcontacorrente_origem);
            CREATE INDEX IF NOT EXISTS idx_transferencia_destino ON transferencia(idcontacorrente_destino);
            CREATE INDEX IF NOT EXISTS idx_transferencia_data ON transferencia(datamovimento);
            CREATE INDEX IF NOT EXISTS idx_transferencia_status ON transferencia(status);
            CREATE INDEX IF NOT EXISTS idx_transferencia_requisicao ON transferencia(identificacao_requisicao);
            CREATE INDEX IF NOT EXISTS idx_idempotencia_chave ON idempotencia(chave_idempotencia);
            CREATE INDEX IF NOT EXISTS idx_idempotencia_data ON idempotencia(data_criacao);
        ";

        var commands = sql.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var commandText in commands.Where(c => !string.IsNullOrWhiteSpace(c)))
        {
            var trimmedCommand = commandText.Trim();
            if (!string.IsNullOrEmpty(trimmedCommand))
            {
                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = trimmedCommand;
                    command.ExecuteNonQuery();
                }
                catch (Exception)
                {
                }
            }
        }
    }
    catch (Exception)
    {
        throw;
    }
}

public class TarifaConfig
{
    public decimal ValorTarifa { get; set; } = 2.00m;
}

public class TransferenciaKafkaConfig
{
    public string BootstrapServers { get; set; } = "kafka:9092";
    public string TransferenciasTopic { get; set; } = "transferencias-realizadas";
    public string TarifasTopic { get; set; } = "tarifas-processadas";
}
