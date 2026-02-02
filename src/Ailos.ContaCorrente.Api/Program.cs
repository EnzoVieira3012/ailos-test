// src/Ailos.ContaCorrente.Api/Program.cs

using Ailos.Common.Application.Extensions;
using Ailos.Common.Application.Middleware;
using Ailos.ContaCorrente.Api.Application.Services.Implementations;
using Ailos.ContaCorrente.Api.Application.Services.Interfaces;
using Ailos.ContaCorrente.Api.Infrastructure.Repositories.Implementations;
using Ailos.ContaCorrente.Api.Infrastructure.Repositories.Interfaces;
using Ailos.EncryptedId;
using Ailos.EncryptedId.JsonConverters;
using DotNetEnv;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        "/app/logs/contacorrente-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

try
{
    Log.Information("🚀 Iniciando Ailos Conta Corrente API...");

    // ================= CARREGAR .env =================
    Env.Load();
    Log.Information("✅ Variáveis de ambiente carregadas");

    // Log das variáveis carregadas (sem mostrar valores completos por segurança)
    var encryptedIdSecret = Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET");
    Log.Debug($"ENCRYPTED_ID_SECRET configurado: {!string.IsNullOrEmpty(encryptedIdSecret)}");
    Log.Debug($"JWT_ISSUER: {Environment.GetEnvironmentVariable("JWT_ISSUER")}");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ================= CONFIGURAÇÕES =================
    Log.Debug("Configurando serviços...");

    // Connection string
    var dbConnection = "Data Source=/app/data/ailos.db";
    Log.Information($"Banco de dados: {dbConnection}");

    // ================= AILOS COMMON =================
    // ⚠️ ESTE MÉTODO JÁ CONFIGURA AUTENTICAÇÃO JWT AUTOMATICAMENTE
    // Não configure JWT manualmente aqui!
    builder.Services.AddAilosCommon(builder.Configuration, dbConnection);
    Log.Debug("Serviços Common adicionados (incluindo JWT)");

    // ================= SERVIÇOS DO DOMÍNIO =================
    builder.Services.AddScoped<IContaCorrenteRepository, ContaCorrenteRepository>();
    builder.Services.AddScoped<IMovimentoRepository, MovimentoRepository>();
    builder.Services.AddScoped<IIdempotenciaRepository, IdempotenciaRepository>();
    builder.Services.AddScoped<IContaCorrenteService, ContaCorrenteService>();
    builder.Services.AddScoped<IMovimentacaoService, MovimentacaoService>();
    builder.Services.AddScoped<IIdempotenciaService, IdempotenciaService>();
    Log.Debug("Serviços de domínio adicionados");

    // ================= ENCRYPTED ID =================
    if (string.IsNullOrEmpty(encryptedIdSecret))
    {
        Log.Fatal("❌ ENCRYPTED_ID_SECRET não configurado");
        throw new InvalidOperationException("ENCRYPTED_ID_SECRET não configurado");
    }

    builder.Services.AddSingleton<IEncryptedIdService>(_ =>
        EncryptedIdFactory.CreateService(encryptedIdSecret)
    );
    Log.Debug("EncryptedId configurado");

    // ================= CONTROLLERS =================
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new EncryptedIdJsonConverter()
            );
        });
    Log.Debug("Controllers configurados");

    // ================= SWAGGER =================
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Ailos Conta Corrente API",
            Version = "v1"
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Bearer {token}"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });
    Log.Debug("Swagger configurado");

    // ================= INFRA =================
    builder.Services.AddMemoryCache();
    builder.Services.AddHealthChecks();
    Log.Debug("Serviços de infraestrutura adicionados");

    var app = builder.Build();

    // ================= MIDDLEWARE =================
    app.UseMiddleware<ExceptionMiddleware>();
    Log.Debug("ExceptionMiddleware configurado");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ailos Conta Corrente API v1");
            c.RoutePrefix = string.Empty;
        });
        Log.Debug("Swagger UI habilitado para desenvolvimento");
    }

    // Endpoint de health check SEM autenticação
    app.MapGet("/health", () => Results.Json(new 
    { 
        status = "healthy", 
        timestamp = DateTime.UtcNow,
        service = "conta-corrente-api"
    }));
    
    app.MapGet("/healthz", () => "OK");

    // ⚠️ ORDEM CRÍTICA: Authentication deve vir antes de Authorization
    app.UseAuthentication();
    app.UseAuthorization();
    
    app.MapControllers();
    Log.Information("✅ Middleware configurado");

    // ================= INICIALIZAR BANCO =================
    Log.Information("🔄 Inicializando banco de dados...");
    await InitializeDatabase(app.Services);

    Log.Information("✅ Ailos Conta Corrente API pronta! URL: http://localhost:8080");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Aplicação falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// ================= FUNÇÃO PARA INICIALIZAR BANCO =================
static async Task InitializeDatabase(IServiceProvider services)
{
    try
    {
        using var scope = services.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<Ailos.Common.Infrastructure.Data.IDbConnectionFactory>();

        using var connection = connectionFactory.CreateConnection();
        connection.Open();

        Log.Information("🔗 Conexão com banco de dados aberta");

        // Criar tabelas se não existirem
        var sql = @"
            CREATE TABLE IF NOT EXISTS contacorrente (
                idcontacorrente INTEGER PRIMARY KEY AUTOINCREMENT,
                cpf TEXT NOT NULL UNIQUE,
                numero INTEGER NOT NULL UNIQUE,
                nome TEXT NOT NULL,
                ativo INTEGER NOT NULL DEFAULT 1,
                senha_hash TEXT NOT NULL,
                data_criacao TEXT NOT NULL DEFAULT (datetime('now')),
                data_atualizacao TEXT,
                CHECK (ativo IN (0, 1))
            );

            CREATE TABLE IF NOT EXISTS movimento (
                idmovimento INTEGER PRIMARY KEY AUTOINCREMENT,
                idcontacorrente INTEGER NOT NULL,
                datamovimento TEXT NOT NULL DEFAULT (datetime('now')),
                tipomovimento TEXT NOT NULL,
                valor REAL NOT NULL,
                descricao TEXT,
                identificacao_requisicao TEXT,
                CHECK (tipomovimento IN ('C', 'D')),
                FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
            );

            CREATE TABLE IF NOT EXISTS idempotencia (
                chave_idempotencia TEXT PRIMARY KEY,
                requisicao TEXT,
                resultado TEXT,
                data_criacao TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- Índices
            CREATE INDEX IF NOT EXISTS idx_conta_cpf ON contacorrente(cpf);
            CREATE INDEX IF NOT EXISTS idx_conta_numero ON contacorrente(numero);
            CREATE INDEX IF NOT EXISTS idx_movimento_conta ON movimento(idcontacorrente);
            CREATE INDEX IF NOT EXISTS idx_movimento_data ON movimento(datamovimento);
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

                    Log.Debug($"Executado: {trimmedCommand.Substring(0, Math.Min(50, trimmedCommand.Length))}...");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"⚠️ Comando SQL ignorado: {ex.Message}");
                }
            }
        }

        Log.Information($"✅ Banco inicializado: {executed} comandos executados");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ ERRO ao inicializar banco");
        throw;
    }
}