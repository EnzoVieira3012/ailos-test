using Ailos.ContaCorrente.Api.Application.Services;
using Ailos.ContaCorrente.Api.Infrastructure.Data;
using Ailos.ContaCorrente.Api.Infrastructure.Repositories;
using Ailos.ContaCorrente.Api.Infrastructure.Security;
using Ailos.ContaCorrente.Api.Presentation.Filters;
using Ailos.EncryptedId;
using Ailos.EncryptedId.JsonConverters;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Carregar variáveis de ambiente do arquivo .env
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Configurar Configuration para usar variáveis de ambiente
builder.Configuration.AddEnvironmentVariables();

// Banco de dados SQLite
builder.Services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();

// Repositórios
builder.Services.AddScoped<IContaCorrenteRepository, ContaCorrenteRepository>();
builder.Services.AddScoped<IMovimentoRepository, MovimentoRepository>();
builder.Services.AddScoped<IIdempotenciaRepository, IdempotenciaRepository>();

// Services
builder.Services.AddScoped<IContaCorrenteService, ContaCorrenteService>();
builder.Services.AddScoped<IMovimentacaoService, MovimentacaoService>();
builder.Services.AddScoped<IIdempotenciaService, IdempotenciaService>();

// Encrypted ID Service - agora pega do .env
var encryptedIdSecret = Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET")
    ?? throw new InvalidOperationException("ENCRYPTED_ID_SECRET não configurada no .env");
builder.Services.AddSingleton<IEncryptedIdService>(_ => 
    Ailos.EncryptedId.EncryptedIdFactory.CreateService(encryptedIdSecret));

// JWT Authentication - agora pega do .env
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
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
});

// Controllers com filtros
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiExceptionFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new EncryptedIdJsonConverter());
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Ailos Conta Corrente API", Version = "v1" });
    
    // Configurar JWT no Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando o esquema Bearer.",
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

// Cache em memória
builder.Services.AddMemoryCache();

// Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // Inicializar banco de dados em desenvolvimento
    await InitializeDatabase(app.Services);
}

app.UseHttpsRedirection();

// Health Check endpoint
app.MapHealthChecks("/health");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task InitializeDatabase(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    
    using var connection = connectionFactory.CreateConnection();
    connection.Open();
    
    try
    {
        // Executar scripts SQL
        var sqlPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts/sql/contacorrente.sql");
        
        if (File.Exists(sqlPath))
        {
            var sql = await File.ReadAllTextAsync(sqlPath);
            
            // Executar cada comando separadamente
            var commands = sql.Split(';', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var commandText in commands)
            {
                if (!string.IsNullOrWhiteSpace(commandText))
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = commandText.Trim();
                    try
                    {
                        command.ExecuteNonQuery();
                        Console.WriteLine($"Comando executado: {commandText.Substring(0, Math.Min(50, commandText.Length))}...");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Aviso ao executar comando SQL: {ex.Message}");
                    }
                }
            }
            Console.WriteLine("Banco de dados inicializado com sucesso!");
        }
        else
        {
            Console.WriteLine($"Arquivo SQL não encontrado em: {sqlPath}");
            // Criar estrutura básica se o arquivo não existir
            await CreateBasicTables(connection);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao inicializar banco de dados: {ex.Message}");
    }
}

static async Task CreateBasicTables(System.Data.IDbConnection connection)
{
    // Criar tabelas básicas se o script SQL não existir
    var createTablesSql = @"
        CREATE TABLE IF NOT EXISTS contacorrente (
            idcontacorrente INTEGER PRIMARY KEY AUTOINCREMENT,
            cpf TEXT NOT NULL UNIQUE,
            numero INTEGER NOT NULL UNIQUE,
            nome TEXT NOT NULL,
            ativo INTEGER NOT NULL DEFAULT 1,
            senha_hash TEXT NOT NULL,
            salt TEXT NOT NULL,
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
            CHECK (tipomovimento IN ('C', 'D')),
            FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS idempotencia (
            chave_idempotencia TEXT PRIMARY KEY,
            requisicao TEXT,
            resultado TEXT,
            data_criacao TEXT NOT NULL DEFAULT (datetime('now'))
        );

        CREATE INDEX IF NOT EXISTS idx_conta_cpf ON contacorrente(cpf);
        CREATE INDEX IF NOT EXISTS idx_conta_numero ON contacorrente(numero);
        CREATE INDEX IF NOT EXISTS idx_movimento_conta ON movimento(idcontacorrente);
        CREATE INDEX IF NOT EXISTS idx_movimento_data ON movimento(datamovimento);
        CREATE INDEX IF NOT EXISTS idx_idempotencia_chave ON idempotencia(chave_idempotencia);
        CREATE INDEX IF NOT EXISTS idx_idempotencia_data ON idempotencia(data_criacao);
    ";
    
    var commands = createTablesSql.Split(';', StringSplitOptions.RemoveEmptyEntries);
    
    foreach (var commandText in commands)
    {
        if (!string.IsNullOrWhiteSpace(commandText))
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText.Trim();
            try
            {
                command.ExecuteNonQuery();
                Console.WriteLine($"Tabela criada: {commandText.Substring(0, Math.Min(50, commandText.Length))}...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao criar tabela: {ex.Message}");
            }
        }
    }
    Console.WriteLine("Tabelas básicas criadas com sucesso!");
}
