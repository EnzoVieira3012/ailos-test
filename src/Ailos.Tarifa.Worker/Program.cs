using Ailos.Tarifa.Worker;
using Ailos.Tarifa.Worker.Application.Services;
using Ailos.Tarifa.Worker.Infrastructure.Clients;
using Ailos.Tarifa.Worker.Infrastructure.Kafka;
using Ailos.Tarifa.Worker.Infrastructure.Repositories;
using Ailos.Tarifa.Worker.Infrastructure.Repositories.Implementations;
using Ailos.Common.Infrastructure.Data;
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
    builder.Services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(dbConnection));

    // 2. Configurações Kafka
    Log.Information("📡 Configurando Kafka...");
    var kafkaConfig = new KafkaConfig
    {
        BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:9092",
        TransferenciasTopic = Environment.GetEnvironmentVariable("KAFKA_TRANSFERENCIAS_TOPIC") ?? "transferencias-realizadas",
        TarifasTopic = Environment.GetEnvironmentVariable("KAFKA_TARIFAS_TOPIC") ?? "tarifas-processadas",
        ConsumerGroup = Environment.GetEnvironmentVariable("KAFKA_CONSUMER_GROUP") ?? "tarifa-worker-group"
    };
    
    builder.Services.AddSingleton(kafkaConfig);
    Log.Information("✅ Kafka configurado - Servers: {Servers}, Tópico: {Topic}, Group: {Group}", 
        kafkaConfig.BootstrapServers, kafkaConfig.TransferenciasTopic, kafkaConfig.ConsumerGroup);

    // 3. Configurações de Tarifa
    var tarifaConfig = new TarifaConfig
    {
        ValorTarifaMinima = 0.01m,
        MaxTentativas = 3,
        DelayEntreTentativasMs = 1000
    };
    builder.Services.AddSingleton(tarifaConfig);
    Log.Information("💰 Configuração de tarifa: MaxTentativas={MaxTentativas}, Delay={Delay}ms", 
        tarifaConfig.MaxTentativas, tarifaConfig.DelayEntreTentativasMs);

    // 4. HTTP Client para Conta Corrente API
    Log.Information("🔗 Configurando cliente HTTP...");
    var contaCorrenteApiUrl = Environment.GetEnvironmentVariable("CONTA_CORRENTE_API_URL") 
        ?? "http://conta-corrente-api:80";
    
    builder.Services.AddHttpClient<IContaCorrenteClient, ContaCorrenteClient>((provider, client) =>
    {
        client.BaseAddress = new Uri(contaCorrenteApiUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        Log.Debug("HTTP Client configurado para: {BaseUrl}", contaCorrenteApiUrl);
    });

    // 5. Repositórios - CORRIGIDO
    Log.Debug("Registrando repositórios...");
    builder.Services.AddScoped<ITarifaRepository, TarifaRepository>();

    // 6. Serviços
    Log.Debug("Registrando serviços...");
    builder.Services.AddScoped<ITarifaProcessor, TarifaProcessor>();
    builder.Services.AddScoped<IKafkaConsumerService, KafkaConsumerService>();
    builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();

    // 7. Worker
    builder.Services.AddHostedService<Worker>();
    Log.Information("👷 Worker registrado como serviço hospedado");

    var host = builder.Build();
    
    Log.Information("🏗️ Host construído com sucesso");

    // ================= INICIALIZAR BANCO DE DADOS =================
    Log.Information("🔄 Inicializando banco de dados...");
    await InitializeDatabase(host.Services);
    
    Log.Information("✅ Banco de dados inicializado");

    // ================= INICIAR HOST =================
    Log.Information("🚀 AILOS TARIFA WORKER INICIADO COM SUCESSO!");
    Log.Information("📡 Consumindo tópico: {Topic}", kafkaConfig.TransferenciasTopic);
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
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();

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
        command.ExecuteNonQuery();
        
        logger.LogInformation("✅ Tabelas de tarifa criadas/verificadas");
        
        // Contar registros existentes
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
