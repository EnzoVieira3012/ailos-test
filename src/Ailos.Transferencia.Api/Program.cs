using Ailos.Common.Application.Extensions;
using Ailos.Common.Application.Middleware;
using Ailos.Common.Infrastructure.Data;
using Ailos.Common.Messaging;
using Ailos.Common.Presentation.Middleware;
using Ailos.EncryptedId;
using Ailos.EncryptedId.JsonConverters;
using Ailos.Transferencia.Api.Application.Services;
using Ailos.Transferencia.Api.Infrastructure.Clients;
using Ailos.Transferencia.Api.Infrastructure.Repositories;
using Ailos.Transferencia.Api.Infrastructure.Repositories.Implementations;
using DotNetEnv;
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

    // 🔥 🔥 🔥 CORREÇÃO CRÍTICA: FORÇAR VALORES CORRETOS DO JWT 🔥 🔥 🔥
    // O problema é que o método AddAilosCommon está pegando valores errados do appsettings.json
    // Vamos sobrescrever com os valores corretos antes de configurar os serviços
    Environment.SetEnvironmentVariable("JWT_AUDIENCE", "AilosClients");
    Environment.SetEnvironmentVariable("JWT_ISSUER", "AilosBankingSystem");
    // Garantir que o JWT_SECRET também está definido
    var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
    if (string.IsNullOrEmpty(jwtSecret))
    {
        Log.Error("❌ JWT_SECRET não configurado no .env");
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

    Log.Information("✅ Variáveis de ambiente carregadas: {@EnvVars}", envVars);

    // 🔥 VERIFICAÇÃO EXTRA: Log dos valores JWT que serão usados
    Log.Information("🔐 CONFIGURAÇÃO JWT PARA TRANSFERÊNCIA API:");
    Log.Information("   Issuer: {Issuer}", Environment.GetEnvironmentVariable("JWT_ISSUER"));
    Log.Information("   Audience: {Audience}", Environment.GetEnvironmentVariable("JWT_AUDIENCE"));
    Log.Information("   Secret configurado: {HasSecret}", !string.IsNullOrEmpty(jwtSecret));

    var builder = WebApplication.CreateBuilder(args);

    // 🔥 USAR SERILOG
    builder.Host.UseSerilog();

    // ================= CONFIGURAÇÕES =================
    Log.Debug("Configurando serviços da aplicação...");

    // 1. Connection String do banco
    var dbConnection = "Data Source=/app/data/transferencia.db";
    Log.Information("💾 Banco de dados: {DatabasePath}", dbConnection);

    // 🔥 REMOVER CONFIGURAÇÕES JWT DO APPSETTINGS PARA EVITAR CONFLITOS
    // O appsettings.json pode ter valores hardcoded que causam o problema
    builder.Configuration["Jwt:Audience"] = null;
    builder.Configuration["Jwt:Issuer"] = null;
    builder.Configuration["Jwt:Secret"] = null;

    // 2. Configurar Common com JWT e banco
    Log.Information("🔐 Configurando autenticação JWT...");
    builder.Services.AddAilosCommon(builder.Configuration, dbConnection);
    Log.Information("✅ Common configurado com JWT e banco de dados");

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
    builder.Services.AddAilosKafka(builder.Configuration);
    Log.Information("✅ Kafka configurado via Ailos.Common");

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

    // ================= CONTROLLERS =================
    builder.Services.AddControllers()
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
    builder.Services.AddHealthChecks();
    Log.Debug("Serviços de infraestrutura configurados");

    // ================= CONSTRUIR APLICAÇÃO =================
    var app = builder.Build();

    Log.Information("🏗️ Aplicação construída com sucesso");

    // ================= MIDDLEWARE PIPELINE =================
    Log.Debug("Configurando pipeline de middleware...");

    // 🔥 1️⃣ Routing PRIMEIRO
    app.UseRouting();

    // 2️⃣ Middlewares customizados
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ExceptionMiddleware>();

    // 3️⃣ Swagger
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ailos Transferência API v1");
        c.RoutePrefix = string.Empty; // Swagger em /
        c.DisplayRequestDuration();
    });

    Log.Information("📚 Swagger habilitado (forçado)");

    // 4️⃣ Auth
    app.UseAuthentication();
    app.UseAuthorization();

    // 5️⃣ Endpoints
    app.MapControllers();

    app.MapGet("/health", () => Results.Json(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        service = "transferencia-api",
        database = "connected",
        kafka = "configured",
        jwt_configured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_SECRET"))
    }));

    app.MapGet("/healthz", () => "OK");

    Log.Information("❤️ Health check disponível em /health");

    // ================= INICIALIZAR BANCO DE DADOS =================
    Log.Information("🔄 Inicializando banco de dados...");
    await InitializeDatabase(app.Services);

    Log.Information("✅ Banco de dados inicializado");

    // ================= VERIFICAÇÃO FINAL JWT =================
    // Obter as configurações JWT para confirmar
    using var scope = app.Services.CreateScope();
    try
    {
        var jwtSettings = scope.ServiceProvider.GetService<Ailos.Common.Configuration.JwtSettings>();
        if (jwtSettings != null)
        {
            Log.Information("🔐 CONFIGURAÇÃO JWT FINAL:");
            Log.Information("   Issuer: {Issuer}", jwtSettings.Issuer);
            Log.Information("   Audience: {Audience}", jwtSettings.Audience);
            Log.Information("   Secret definido: {HasSecret}", !string.IsNullOrEmpty(jwtSettings.Secret));
            
            if (jwtSettings.Audience != "AilosClients")
            {
                Log.Warning("⚠️ Audience incorreto: {Audience}. Deveria ser 'AilosClients'", jwtSettings.Audience);
            }
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Não foi possível verificar configurações JWT");
    }

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
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        using var connection = connectionFactory.CreateConnection();
        connection.Open();

        logger.LogInformation("🔗 Conexão com banco de dados aberta");

        // SQL para criar tabelas de transferência
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

        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = @"
            SELECT name FROM sqlite_master 
            WHERE type='table' 
            AND name IN ('transferencia', 'idempotencia')
            ORDER BY name";

        using var reader = checkCommand.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        logger.LogInformation("📊 Tabelas existentes: {@Tables}", tables);
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

// MUDOU AQUI: Renomeei para evitar conflito com KafkaConfig do Common
public class TransferenciaKafkaConfig
{
    public string BootstrapServers { get; set; } = "kafka:9092";
    public string TransferenciasTopic { get; set; } = "transferencias-realizadas";
    public string TarifasTopic { get; set; } = "tarifas-processadas";
}

// ================= MIDDLEWARE DE LOGGING =================