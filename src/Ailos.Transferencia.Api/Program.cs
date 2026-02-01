using Ailos.Transferencia.Api.Application.Services;
using Ailos.Transferencia.Api.Infrastructure.Clients;
using Ailos.Transferencia.Api.Infrastructure.Kafka;
using Ailos.Transferencia.Api.Infrastructure.Repositories;
using Ailos.Transferencia.Api.Infrastructure.Repositories.Implementations;
using Ailos.EncryptedId;
using Ailos.EncryptedId.JsonConverters;
using Ailos.Common.Infrastructure.Data;
using Ailos.Common.Infrastructure.Security;
using Ailos.Common.Configuration;
using Ailos.Common.Infrastructure.Security.Extensions;
using Ailos.Common.Presentation.Filters;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using Serilog.Events;

// 🔥 CONFIGURAÇÃO DE LOGS DETALHADA
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
    Log.Information("🚀 =========================================");
    Log.Information("🚀 INICIANDO AILOS TRANSFERÊNCIA API");
    Log.Information("🚀 =========================================");

    // ================= CARREGAR .env =================
    Log.Information("📁 Carregando variáveis de ambiente...");
    Env.Load();
    
    // Log de variáveis críticas (parciais por segurança)
    var envVars = new
    {
        EncryptedIdLoaded = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET")),
        JwtSecretLoaded = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_SECRET")),
        KafkaServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS"),
        ContaApiUrl = Environment.GetEnvironmentVariable("CONTA_CORRENTE_API_URL")
    };
    
    Log.Information("✅ Variáveis de ambiente carregadas: {@EnvVars}", envVars);

    var builder = WebApplication.CreateBuilder(args);
    
    // 🔥 USAR SERILOG
    builder.Host.UseSerilog();
    
    // ================= CONFIGURAÇÕES =================
    Log.Debug("Configurando serviços da aplicação...");
    
    // 1. Banco de Dados
    var dbConnection = "Data Source=/app/data/transferencia.db";
    Log.Information("💾 Banco de dados: {DatabasePath}", dbConnection);
    builder.Services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(dbConnection));

    // 2. JWT - Carregar APENAS de variáveis de ambiente
    Log.Information("🔐 Configurando autenticação JWT...");
    var jwtSettings = new JwtSettings
    {
        Secret = Environment.GetEnvironmentVariable("JWT_SECRET") 
            ?? throw new InvalidOperationException("JWT_SECRET não configurado"),
        Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
            ?? "AilosBankingSystem",
        Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
            ?? "AilosClients",
        ExpirationMinutes = int.TryParse(Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES"), out var minutes) 
            ? minutes : 60
    };
    
    Log.Information("✅ JWT configurado - Issuer: {Issuer}, Audience: {Audience}, Expiração: {ExpirationMinutes} min", 
        jwtSettings.Issuer, jwtSettings.Audience, jwtSettings.ExpirationMinutes);
    
    // Configurar JWT no DI
    builder.Services.Configure<JwtSettings>(options =>
    {
        options.Secret = jwtSettings.Secret;
        options.Issuer = jwtSettings.Issuer;
        options.Audience = jwtSettings.Audience;
        options.ExpirationMinutes = jwtSettings.ExpirationMinutes;
    });

    // Registrar serviço JWT
    builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
    
    // Configurar autenticação
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.Zero
        };
        
        // Logs detalhados do JWT
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("❌ Autenticação JWT falhou: {ErrorMessage}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var userId = context.Principal?.FindFirst("contaId")?.Value;
                Log.Debug("✅ Token JWT validado para conta: {ContaId}", userId);
                return Task.CompletedTask;
            }
        };
    });
    
    Log.Information("✅ Autenticação JWT configurada");

    // 3. Configurações de negócio
    var tarifaConfig = new TarifaConfig
    {
        ValorTarifa = decimal.TryParse(Environment.GetEnvironmentVariable("TARIFA_VALOR"), out var tarifa) 
            ? tarifa : 2.00m
    };
    builder.Services.AddSingleton(tarifaConfig);
    Log.Information("💰 Tarifa configurada: R$ {ValorTarifa}", tarifaConfig.ValorTarifa);

    // 4. Encrypted ID
    var encryptedIdSecret = Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET")
        ?? throw new InvalidOperationException("ENCRYPTED_ID_SECRET não configurada");
    builder.Services.AddSingleton<IEncryptedIdService>(_ => 
        EncryptedIdFactory.CreateService(encryptedIdSecret));
    Log.Information("🔒 EncryptedID configurado (secret: {SecretLength} chars)", encryptedIdSecret.Length);

    // 5. Kafka
    Log.Information("📡 Configurando Kafka...");
    var kafkaConfig = new KafkaConfig
    {
        BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:9092",
        TransferenciasTopic = Environment.GetEnvironmentVariable("KAFKA_TRANSFERENCIAS_TOPIC") ?? "transferencias-realizadas",
        TarifasTopic = Environment.GetEnvironmentVariable("KAFKA_TARIFAS_TOPIC") ?? "tarifas-processadas"
    };
    
    builder.Services.AddSingleton(kafkaConfig);
    builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
    Log.Information("✅ Kafka configurado - Servers: {Servers}, Tópico: {Topic}", 
        kafkaConfig.BootstrapServers, kafkaConfig.TransferenciasTopic);

    // 6. HTTP Client para Conta Corrente API
    Log.Information("🔗 Configurando cliente HTTP...");
    var contaCorrenteApiUrl = Environment.GetEnvironmentVariable("CONTA_CORRENTE_API_URL") 
        ?? "http://conta-corrente-api:80";
    
    builder.Services.AddHttpClient<IContaCorrenteClient, ContaCorrenteClient>((provider, client) =>
    {
        client.BaseAddress = new Uri(contaCorrenteApiUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        Log.Debug("HTTP Client configurado para: {BaseUrl}", contaCorrenteApiUrl);
    });

    // ================= REPOSITÓRIOS =================
    Log.Debug("Registrando repositórios...");
    builder.Services.AddScoped<ITransferenciaRepository, TransferenciaRepository>();
    builder.Services.AddScoped<IIdempotenciaRepository, IdempotenciaRepository>();

    // ================= SERVIÇOS =================
    Log.Debug("Registrando serviços de aplicação...");
    builder.Services.AddScoped<ITransferenciaService, TransferenciaService>();
    builder.Services.AddScoped<IIdempotenciaService, IdempotenciaService>();

    // ================= FILTRO DE EXCEÇÕES =================
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ApiExceptionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new EncryptedIdJsonConverter());
    });
    
    Log.Debug("Controllers configurados");

    // ================= SWAGGER =================
    Log.Debug("Configurando Swagger/OpenAPI...");
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
    
    Log.Information("📚 Swagger configurado");

    // ================= INFRAESTRUTURA =================
    builder.Services.AddMemoryCache();
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database");
    // .AddUrlGroup(new Uri($"{contaCorrenteApiUrl}/health"), "conta-corrente-api"); // REMOVIDO - Causava erro
    
    Log.Debug("Serviços de infraestrutura configurados");

    // ================= CONSTRUIR APLICAÇÃO =================
    var app = builder.Build();
    
    Log.Information("🏗️ Aplicação construída com sucesso");

    // ================= MIDDLEWARE PIPELINE =================
    Log.Debug("Configurando pipeline de middleware...");
    
    // Logging de todas as requisições
    app.Use(async (context, next) =>
    {
        var startTime = DateTime.UtcNow;
        Log.Debug("➡️ Request: {Method} {Path}", context.Request.Method, context.Request.Path);
        
        await next();
        
        var duration = DateTime.UtcNow - startTime;
        Log.Debug("⬅️ Response: {Method} {Path} - {StatusCode} em {Duration}ms", 
            context.Request.Method, context.Request.Path, context.Response.StatusCode, duration.TotalMilliseconds);
    });

    // Swagger apenas em desenvolvimento
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ailos Transferência API v1");
            c.RoutePrefix = string.Empty; // Swagger na raiz
            c.DisplayRequestDuration();
        });
        Log.Information("🔧 Swagger UI habilitado para desenvolvimento");
    }

    app.UseHttpsRedirection();
    
    // Health Check endpoint
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds
                }),
                timestamp = DateTime.UtcNow
            });
            await context.Response.WriteAsync(result);
        }
    });
    
    Log.Information("❤️ Health check disponível em /health");

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    
    Log.Information("✅ Pipeline de middleware configurado");

    // ================= INICIALIZAR BANCO DE DADOS =================
    Log.Information("🔄 Inicializando banco de dados...");
    await InitializeDatabase(app.Services);
    
    Log.Information("✅ Banco de dados inicializado");

    // ================= INICIAR APLICAÇÃO =================
    Log.Information("🚀 AILOS TRANSFERÊNCIA API INICIADA COM SUCESSO!");
    Log.Information("🌐 URL: http://localhost:5081");
    Log.Information("📚 Swagger: http://localhost:5081");
    Log.Information("❤️ Health: http://localhost:5081/health");
    Log.Information("=========================================");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "💥 APLICAÇÃO FALHOU AO INICIAR");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// ================= FUNÇÕES AUXILIARES =================

static async Task InitializeDatabase(IServiceProvider services)
{
    try
    {
        using var scope = services.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        
        logger.LogInformation("🔗 Conexão com banco de dados aberta");

        // SQL para criar tabelas de transferência
        var sql = @"
            -- Tabela de transferências
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

            -- Tabela de idempotência
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
            CREATE INDEX IF NOT EXISTS idx_idempotencia_chave ON idempotencia(chave_idempotencia);
            CREATE INDEX IF NOT EXISTS idx_idempotencia_data ON idempotencia(data_criacao);
        ";

        var commands = sql.Split(';', StringSplitOptions.RemoveEmptyEntries);
        int executed = 0;

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
                    executed++;
                    
                    logger.LogDebug("📝 SQL executado: {Command}", trimmedCommand.Substring(0, Math.Min(50, trimmedCommand.Length)));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "⚠️ Comando SQL ignorado: {ErrorMessage}", ex.Message);
                }
            }
        }

        logger.LogInformation("✅ Banco de transferência inicializado: {Comandos} comandos executados", executed);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ ERRO CRÍTICO ao inicializar banco de dados");
        throw;
    }
}

// ================= CLASSES DE CONFIGURAÇÃO =================

public class TarifaConfig
{
    public decimal ValorTarifa { get; set; } = 2.00m;
}

public class DatabaseHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IDbConnectionFactory _connectionFactory;
    
    public DatabaseHealthCheck(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var result = command.ExecuteScalar();
            
            return result != null 
                ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Database conectado")
                : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Database não responde");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Erro no database", ex);
        }
    }
}