using Ailos.Tarifa.Worker;
using Ailos.Tarifa.Worker.Application.Services;
using Ailos.Tarifa.Worker.Infrastructure.Clients;
using Ailos.Tarifa.Worker.Infrastructure.Kafka;
using Ailos.Tarifa.Worker.Infrastructure.Repositories;
using Ailos.Tarifa.Worker.Infrastructure.Repositories.Implementations;
using Ailos.Common.Application.Extensions;
using Ailos.Common.Infrastructure.Data;
using DotNetEnv;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Http;
using Polly;
using Microsoft.Extensions.Configuration;

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
        "/app/logs/tarifa-worker-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

try
{
    Log.Information("🚀 =========================================");
    Log.Information("🚀 INICIANDO AILOS TARIFA WORKER");
    Log.Information("🚀 =========================================");

    // ================= CARREGAR .env =================
    Log.Information("📁 Carregando variáveis de ambiente...");
    Env.Load();
    
    // Verificar variáveis críticas
    var envVars = new
    {
        KafkaServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS"),
        ContaApiUrl = Environment.GetEnvironmentVariable("CONTA_CORRENTE_API_URL"),
        KafkaTransferenciasTopic = Environment.GetEnvironmentVariable("KAFKA_TRANSFERENCIAS_TOPIC"),
        KafkaTarifasTopic = Environment.GetEnvironmentVariable("KAFKA_TARIFAS_TOPIC")
    };
    
    Log.Information("✅ Variáveis de ambiente carregadas: {@EnvVars}", envVars);

    var builder = Host.CreateApplicationBuilder(args);
    
    // 🔥 USAR SERILOG - CORRIGIDO
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();
    
    // ================= CONFIGURAÇÕES =================
    Log.Information("⚙️ Configurando serviços...");
    
    // 1. Banco de Dados
    var dbConnection = "Data Source=/app/data/tarifas.db";
    Log.Information("💾 Banco de dados: {DatabasePath}", dbConnection);

    // 2. Configurações de Tarifa
    var tarifaConfig = new TarifaConfig
    {
        ValorTarifaMinima = 0.01m,
        MaxTentativas = 3,
        DelayEntreTentativasMs = 1000
    };
    builder.Services.AddSingleton(tarifaConfig);
    Log.Information("💰 Configuração de tarifa: MaxTentativas={MaxTentativas}, Delay={Delay}ms", 
        tarifaConfig.MaxTentativas, tarifaConfig.DelayEntreTentativasMs);

    // ================= AILOS COMMON & KAFKA =================
    Log.Information("🔧 Adicionando serviços Ailos Common...");
    
    // ⚠️ ADICIONAR AILOS COMMON - Isso adiciona automaticamente:
    // - JWT Authentication (se necessário para chamadas autenticadas)
    // - MemoryCache (para IIdempotencyService)
    // - IIdempotencyService (para idempotência de mensagens)
    // - IDbConnectionFactory (configurado com dbConnection)
    // - IPasswordHasher (se necessário)
    // - ApiExceptionFilter (para tratamento de exceções)
    builder.Services.AddAilosCommon(builder.Configuration, dbConnection);
    Log.Information("✅ Ailos Common configurado");

    // ================= ADICIONAR AILOS KAFKA =================
    Log.Information("📡 Adicionando configuração Kafka...");
    
    // ⚠️ ADICIONAR AILOS KAFKA - Isso adiciona automaticamente:
    // - KafkaConnectionFactory
    // - IKafkaProducerService
    builder.Services.AddAilosKafka(builder.Configuration);
    Log.Information("✅ Kafka configurado via Ailos Common");

    // ================= CONFIGURAR RETRY POLICY PARA KAFKA =================
    Log.Debug("Configurando política de retry para Kafka...");
    var retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryForeverAsync(
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            (exception, timeSpan) =>
            {
                Log.Warning(exception, "❌ Falha na conexão com Kafka. Tentando novamente em {TimeSpan}", timeSpan);
            });

    builder.Services.AddSingleton<IAsyncPolicy>(retryPolicy);

    // 3. HTTP Client para Conta Corrente API
    Log.Information("🔗 Configurando cliente HTTP...");
    var contaCorrenteApiUrl = Environment.GetEnvironmentVariable("CONTA_CORRENTE_API_URL") 
        ?? "http://conta-corrente-api:80";
    
    builder.Services.AddHttpClient<IContaCorrenteClient, ContaCorrenteClient>((provider, client) =>
    {
        client.BaseAddress = new Uri(contaCorrenteApiUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        Log.Debug("HTTP Client configurado para: {BaseUrl}", contaCorrenteApiUrl);
    });

    // 4. Repositórios - CORRIGIDO
    Log.Debug("Registrando repositórios específicos do worker...");
    builder.Services.AddScoped<ITarifaRepository, TarifaRepository>();

    // 5. Serviços específicos do worker
    Log.Debug("Registrando serviços específicos do worker...");
    builder.Services.AddScoped<ITarifaProcessor, TarifaProcessor>();
    builder.Services.AddScoped<IKafkaConsumerService, KafkaConsumerService>();

    // 6. Worker
    builder.Services.AddHostedService<Worker>();
    Log.Information("👷 Worker registrado como serviço hospedado");

    // ================= CONSTRUIR HOST =================
    var host = builder.Build();
    
    Log.Information("🏗️ Host construído com sucesso");

    // Verificar serviços registrados (para debug)
    var serviceProvider = host.Services;
    using (var scope = serviceProvider.CreateScope())
    {
        var sp = scope.ServiceProvider;
        
        // Verificar se IIdempotencyService foi registrado
        try
        {
            var idempotencyService = sp.GetService<Ailos.Common.Infrastructure.Idempotencia.IIdempotenciaService>();
            Log.Information($"✅ IIdempotenciaService registrado: {idempotencyService != null}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ IIdempotenciaService não encontrado ou erro ao obter");
        }

        // Verificar se IKafkaProducerService foi registrado
        try
        {
            var kafkaProducerService = sp.GetService<Ailos.Common.Messaging.IKafkaProducerService>();
            Log.Information($"✅ IKafkaProducerService registrado: {kafkaProducerService != null}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ IKafkaProducerService não encontrado ou erro ao obter");
        }
    }

    // ================= INICIALIZAR BANCO DE DADOS =================
    Log.Information("🔄 Inicializando banco de dados...");
    await InitializeDatabase(host.Services);
    
    Log.Information("✅ Banco de dados inicializado");

    // ================= INICIAR HOST =================
    Log.Information("🚀 AILOS TARIFA WORKER INICIADO COM SUCESSO!");
    Log.Information("📡 Consumindo tópico: transferencias-realizadas");
    Log.Information("👂 Aguardando mensagens Kafka...");
    Log.Information("=========================================");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "💥 WORKER FALHOU AO INICIAR");
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

        // SQL para criar tabelas de tarifa
        var sql = @"
            -- Tabela de tarifas
            CREATE TABLE IF NOT EXISTS tarifa (
                idtarifa INTEGER PRIMARY KEY AUTOINCREMENT,
                idcontacorrente INTEGER NOT NULL,
                idtransferencia INTEGER,
                datamovimento TEXT NOT NULL DEFAULT (datetime('now')),
                valor REAL NOT NULL,
                processada INTEGER NOT NULL DEFAULT 0,
                mensagem_erro TEXT,
                data_processamento TEXT,
                CHECK (processada IN (0, 1))
            );

            -- Tabela de histórico de processamento
            CREATE TABLE IF NOT EXISTS tarifa_processada (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                transferencia_id INTEGER NOT NULL,
                conta_origem_id INTEGER NOT NULL,
                valor_tarifa REAL NOT NULL,
                data_processamento TEXT NOT NULL DEFAULT (datetime('now')),
                status TEXT NOT NULL,
                mensagem TEXT,
                topico_kafka TEXT NOT NULL,
                offset_kafka INTEGER NOT NULL,
                UNIQUE(transferencia_id, topico_kafka, offset_kafka)
            );

            -- Índices para performance
            CREATE INDEX IF NOT EXISTS idx_tarifa_conta ON tarifa(idcontacorrente);
            CREATE INDEX IF NOT EXISTS idx_tarifa_transferencia ON tarifa(idtransferencia);
            CREATE INDEX IF NOT EXISTS idx_tarifa_data ON tarifa(datamovimento);
            CREATE INDEX IF NOT EXISTS idx_tarifa_processada ON tarifa(processada);
            CREATE INDEX IF NOT EXISTS idx_historico_transferencia ON tarifa_processada(transferencia_id);
            CREATE INDEX IF NOT EXISTS idx_historico_conta ON tarifa_processada(conta_origem_id);
            CREATE INDEX IF NOT EXISTS idx_historico_data ON tarifa_processada(data_processamento);
            CREATE INDEX IF NOT EXISTS idx_historico_status ON tarifa_processada(status);
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = command.ExecuteNonQuery();
        
        logger.LogInformation("✅ Tabelas de tarifa criadas/verificadas. Resultado: {Result}", result);
        
        // Contar registros existentes
        try
        {
            using var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM tarifa";
            var tarifaCount = countCommand.ExecuteScalar();
            
            countCommand.CommandText = "SELECT COUNT(*) FROM tarifa_processada";
            var historicoCount = countCommand.ExecuteScalar();
            
            logger.LogInformation("📊 Estatísticas - Tarifas: {TarifaCount}, Histórico: {HistoricoCount}", 
                tarifaCount, historicoCount);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "ℹ️ Tabelas ainda vazias ou erro na contagem");
        }
        
        // Verificar se as tabelas foram criadas
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = @"
            SELECT name FROM sqlite_master 
            WHERE type='table' 
            AND name IN ('tarifa', 'tarifa_processada')
            ORDER BY name";
        
        using var reader = checkCommand.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }
        
        logger.LogInformation("📊 Tabelas de tarifa existentes: {@Tables}", tables);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ ERRO CRÍTICO ao inicializar banco de dados do tarifa worker");
        throw;
    }
}

// ================= CONFIGURAÇÕES =================

public class TarifaConfig
{
    public decimal ValorTarifaMinima { get; set; } = 0.01m;
    public int MaxTentativas { get; set; } = 3;
    public int DelayEntreTentativasMs { get; set; } = 1000;
}

// ================= MIDDLEWARE DE LOGGING =================

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        var request = context.Request;
        
        _logger.LogInformation("➡️ Request recebida: {Method} {Path} {QueryString}", 
            request.Method, request.Path, request.QueryString);
        
        try
        {
            await _next(context);
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("⬅️ Response enviada: {Method} {Path} - {StatusCode} em {Duration}ms", 
                request.Method, request.Path, context.Response.StatusCode, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "❌ Erro durante request: {Method} {Path} - Falhou após {Duration}ms", 
                request.Method, request.Path, duration.TotalMilliseconds);
            throw;
        }
    }
}