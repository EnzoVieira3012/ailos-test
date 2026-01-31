using Ailos.Tarifa.Worker;
using Ailos.Tarifa.Worker.Application.Services;
using Ailos.Tarifa.Worker.Infrastructure.Clients;
using Ailos.Tarifa.Worker.Infrastructure.Data;
using Ailos.Tarifa.Worker.Infrastructure.Kafka;
using Ailos.Tarifa.Worker.Infrastructure.Repositories;
using DotNetEnv;
using Microsoft.Extensions.Options;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

Env.Load();

// Configurar Serilog para logging estruturado
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/tarifa-worker-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Iniciando Worker de Tarifas...");

    var builder = Host.CreateApplicationBuilder(args);

    // Configuração
    builder.Configuration.AddEnvironmentVariables();
    
    // Logging com Serilog
    builder.Services.AddSerilog();
    
    // Database
    builder.Services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
    
    // Repositórios
    builder.Services.AddScoped<ITarifaRepository, TarifaRepository>();
    
    // HTTP Client para API Conta Corrente
    builder.Services.AddHttpClient<IContaCorrenteClient, ContaCorrenteClient>(client =>
    {
        var baseUrl = builder.Configuration["ContaCorrenteApi:BaseUrl"] 
            ?? "http://conta-corrente-api:80";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("User-Agent", "Ailos-Tarifa-Worker");
    });
    
    // Kafka
    var kafkaConfig = new KafkaConfig();
    builder.Configuration.GetSection("Kafka").Bind(kafkaConfig);
    builder.Services.AddSingleton(kafkaConfig);
    
    builder.Services.AddSingleton<IKafkaConsumerService, KafkaConsumerService>();
    builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
    
    // Services
    builder.Services.AddScoped<ITarifaProcessor, TarifaProcessor>();
    
    // Configurações
    var tarifaConfig = new TarifaConfig();
    builder.Configuration.GetSection("TarifaConfig").Bind(tarifaConfig);
    builder.Services.AddSingleton(tarifaConfig);
    
    // Worker
    builder.Services.AddHostedService<Worker>();
    
    var host = builder.Build();

    // Inicializar banco de dados
    await InitializeDatabase(host.Services);
    
    // Configurar graceful shutdown
    var applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    applicationLifetime.ApplicationStopping.Register(() =>
    {
        Log.Information("Aplicação está parando...");
    });
    
    applicationLifetime.ApplicationStopped.Register(() =>
    {
        Log.Information("Aplicação parada");
        Log.CloseAndFlush();
    });
    
    // Executar worker
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicação terminou inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}

static async Task InitializeDatabase(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    
    using var connection = connectionFactory.CreateConnection();
    connection.Open();
    
    var sqlPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts/sql/tarifa.sql");
    
    if (File.Exists(sqlPath))
    {
        try
        {
            var sql = await File.ReadAllTextAsync(sqlPath);
            var commands = sql.Split(';', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var commandText in commands)
            {
                if (!string.IsNullOrWhiteSpace(commandText))
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = commandText.Trim();
                    command.ExecuteNonQuery();
                }
            }
            
            Log.Information("Banco de dados de tarifas inicializado com sucesso!");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao inicializar banco de dados de tarifas");
        }
    }
    else
    {
        Log.Warning("Arquivo SQL de tarifas não encontrado em: {Path}", sqlPath);
    }
}
