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
    Env.Load();

    var encryptedIdSecret = Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var dbConnection = "Data Source=/app/data/ailos.db";

    builder.Services.AddAilosCommon(builder.Configuration, dbConnection);

    builder.Services.AddScoped<IContaCorrenteRepository, ContaCorrenteRepository>();
    builder.Services.AddScoped<IMovimentoRepository, MovimentoRepository>();
    builder.Services.AddScoped<IIdempotenciaRepository, IdempotenciaRepository>();
    builder.Services.AddScoped<IContaCorrenteService, ContaCorrenteService>();
    builder.Services.AddScoped<IMovimentacaoService, MovimentacaoService>();
    builder.Services.AddScoped<IIdempotenciaService, IdempotenciaService>();

    if (string.IsNullOrEmpty(encryptedIdSecret))
    {
        throw new InvalidOperationException("ENCRYPTED_ID_SECRET não configurado");
    }

    builder.Services.AddSingleton<IEncryptedIdService>(_ =>
        EncryptedIdFactory.CreateService(encryptedIdSecret)
    );

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new EncryptedIdJsonConverter()
            );
        });

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

    builder.Services.AddMemoryCache();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseMiddleware<ExceptionMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ailos Conta Corrente API v1");
            c.RoutePrefix = string.Empty;
        });
    }

    app.MapGet("/health", () => Results.Json(new 
    { 
        status = "healthy", 
        timestamp = DateTime.UtcNow,
        service = "conta-corrente-api"
    }));
    
    app.MapGet("/healthz", () => "OK");

    app.UseAuthentication();
    app.UseAuthorization();
    
    app.MapControllers();

    await InitializeDatabase(app.Services);

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
        var connectionFactory = scope.ServiceProvider.GetRequiredService<Ailos.Common.Infrastructure.Data.IDbConnectionFactory>();

        using var connection = connectionFactory.CreateConnection();
        connection.Open();

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
                role TEXT DEFAULT 'conta-corrente',
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