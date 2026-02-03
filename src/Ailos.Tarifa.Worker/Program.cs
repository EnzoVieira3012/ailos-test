using Ailos.Tarifa.Worker.Application.Services;
using Ailos.Tarifa.Worker.Infrastructure.Clients;
using Ailos.Tarifa.Worker.Infrastructure.Kafka;
using Ailos.Tarifa.Worker.Infrastructure.Repositories;
using Ailos.Tarifa.Worker.Infrastructure.Repositories.Implementations;
using Ailos.Common.Infrastructure.Data;
using Ailos.Common.Configuration;
using DotNetEnv;
using Serilog;
using Serilog.Events;
using Ailos.Tarifa.Worker;

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
    
    var envVars = new
    {
        KafkaServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS"),
        ContaApiUrl = Environment.GetEnvironmentVariable("CONTA_CORRENTE_API_URL"),
        KafkaTransferenciasTopic = Environment.GetEnvironmentVariable("KAFKA_TRANSFERENCIAS_TOPIC"),
        KafkaTarifasTopic = Environment.GetEnvironmentVariable("KAFKA_TARIFAS_TOPIC"),
        JwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "Não configurado - OK para worker"
    };
    
    Log.Information("✅ Variáveis de ambiente carregadas: {@EnvVars}", envVars);

    var builder = Host.CreateApplicationBuilder(args);
    
    // 🔥 USAR SERILOG
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog();
    
    // ================= CONFIGURAÇÕES =================
    Log.Information("⚙️ Configurando serviços...");
    
    // 1. Banco de Dados (APENAS para tarifas)
    var dbConnection = "Data Source=/app/data/tarifas.db";
    Log.Information("💾 Banco de dados de tarifas: {DatabasePath}", dbConnection);
    builder.Services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(dbConnection));

    // 2. Configurações Kafka usando KafkaSettings do Common
    Log.Information("📡 Configurando Kafka usando KafkaSettings do Common...");
    
    var kafkaSettings = new KafkaSettings
    {
        // Primeiro carrega das variáveis de ambiente
        BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "kafka:9092",
        TransferenciasTopic = Environment.GetEnvironmentVariable("KAFKA_TRANSFERENCIAS_TOPIC") ?? "transferencias-realizadas",
        TarifasTopic = Environment.GetEnvironmentVariable("KAFKA_TARIFAS_TOPIC") ?? "tarifas-processadas",
        ConsumerGroup = Environment.GetEnvironmentVariable("KAFKA_CONSUMER_GROUP") ?? "tarifa-worker-group"
    };

    // Também pode carregar do appsettings.json se necessário
    builder.Configuration.GetSection(KafkaSettings.SectionName).Bind(kafkaSettings);
    
    // 🔥 REGISTRAR AMBAS AS CONFIGURAÇÕES PARA COMPATIBILIDADE
    builder.Services.AddSingleton(kafkaSettings);
    
    // 🔥 REGISTRAR TAMBÉM COMO KafkaConfig (para compatibilidade com serviços existentes)
    var kafkaConfig = new KafkaConfig
    {
        BootstrapServers = kafkaSettings.BootstrapServers,
        TransferenciasTopic = kafkaSettings.TransferenciasTopic,
        TarifasTopic = kafkaSettings.TarifasTopic,
        ConsumerGroup = kafkaSettings.ConsumerGroup
    };
    builder.Services.AddSingleton(kafkaConfig);
    
    Log.Information("✅ Kafka configurado - Servers: {Servers}, Tópico Transferências: {TransferenciasTopic}, Tópico Tarifas: {TarifasTopic}, Grupo: {ConsumerGroup}", 
        kafkaSettings.BootstrapServers, kafkaSettings.TransferenciasTopic, kafkaSettings.TarifasTopic, kafkaSettings.ConsumerGroup);

    // 3. Configurações de Tarifa
    var tarifaConfig = new TarifaConfig
    {
        ValorTarifaMinima = 0.01m,
        MaxTentativas = 3,
        DelayEntreTentativasMs = 1000
    };
    builder.Services.AddSingleton(tarifaConfig);

    // 4. HTTP Client para Conta Corrente API
    Log.Information("🔗 Configurando cliente HTTP...");
    var contaCorrenteApiUrl = Environment.GetEnvironmentVariable("CONTA_CORRENTE_API_URL")
        ?? "http://conta-corrente-api:80";
    
    builder.Services.AddHttpClient<IContaCorrenteClient, ContaCorrenteClient>((provider, client) =>
    {
        client.BaseAddress = new Uri(contaCorrenteApiUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "Ailos-Tarifa-Worker/1.0");
        Log.Debug("HTTP Client configurado para: {BaseUrl}", contaCorrenteApiUrl);
    });

    // 5. Repositórios
    Log.Debug("Registrando repositórios...");
    builder.Services.AddScoped<ITarifaRepository, TarifaRepository>();

    // 6. Serviços
    Log.Debug("Registrando serviços...");
    builder.Services.AddScoped<ITarifaProcessor, TarifaProcessor>();
    builder.Services.AddScoped<IKafkaConsumerService, KafkaConsumerService>();
    builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();

    // 7. Worker
    builder.Services.AddHostedService<Worker>();
    Log.Information("👷 Worker registrado");

    // ================= CONSTRUIR HOST =================
    var host = builder.Build();
    
    Log.Information("🏗️ Host construído com sucesso");

    // ================= INICIALIZAR BANCO DE DADOS =================
    Log.Information("🔄 Inicializando banco de dados de tarifas...");
    await InitializeDatabase(host.Services);
    
    Log.Information("✅ Banco de dados inicializado");

    // ================= INICIAR HOST =================
    Log.Information("🚀 AILOS TARIFA WORKER INICIADO COM SUCESSO!");
    Log.Information("📡 Consumindo tópico: {Topic}", kafkaSettings.TransferenciasTopic);
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

        // SQL simplificado para tarifas
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
                status TEXT NOT NULL DEFAULT 'SUCESSO',
                mensagem TEXT,
                topico_kafka TEXT NOT NULL,
                offset_kafka INTEGER NOT NULL
            );

            -- Índices básicos
            CREATE INDEX IF NOT EXISTS idx_tarifa_conta ON tarifa(idcontacorrente);
            CREATE INDEX IF NOT EXISTS idx_tarifa_transferencia ON tarifa(idtransferencia);
            CREATE INDEX IF NOT EXISTS idx_historico_transferencia ON tarifa_processada(transferencia_id);
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
        
        logger.LogInformation("✅ Tabelas de tarifa criadas/verificadas");
        
        // Verificar tabelas
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = @"
            SELECT name FROM sqlite_master 
            WHERE type='table' 
            AND name IN ('tarifa', 'tarifa_processada')";
        
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
        Log.Error(ex, "❌ ERRO ao inicializar banco de dados do tarifa worker");
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

// 🔥 CLASSE LOCAL PARA COMPATIBILIDADE
// KafkaConsumerService ainda espera esta classe
public class KafkaConfig
{
    public string BootstrapServers { get; set; } = "kafka:9092";
    public string TransferenciasTopic { get; set; } = "transferencias-realizadas";
    public string TarifasTopic { get; set; } = "tarifas-processadas";
    public string ConsumerGroup { get; set; } = "tarifa-worker-group";
}