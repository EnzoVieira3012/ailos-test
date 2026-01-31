using Ailos.Transferencia.Api.Application.Services;
using Ailos.Transferencia.Api.Infrastructure.Clients;
using Ailos.Transferencia.Api.Infrastructure.Data;
using Ailos.Transferencia.Api.Infrastructure.Kafka;
using Ailos.Transferencia.Api.Infrastructure.Repositories;
using Ailos.Transferencia.Api.Infrastructure.Security;
using Ailos.EncryptedId;
using Ailos.EncryptedId.JsonConverters;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Ailos.Transferencia.Api.Infrastructure.Middleware;

Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Database
builder.Services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();

// Repositories
builder.Services.AddScoped<ITransferenciaRepository, TransferenciaRepository>();
builder.Services.AddScoped<IIdempotenciaRepository, IdempotenciaRepository>();

// Services
builder.Services.AddScoped<ITransferenciaService, TransferenciaService>();
builder.Services.AddScoped<IIdempotenciaService, IdempotenciaService>();

// Encrypted ID
var encryptedIdSecret = Environment.GetEnvironmentVariable("ENCRYPTED_ID_SECRET")
    ?? throw new InvalidOperationException("ENCRYPTED_ID_SECRET não configurada");
builder.Services.AddSingleton<IEncryptedIdService>(_ => 
    EncryptedIdFactory.CreateService(encryptedIdSecret));

// JWT
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

// HTTP Client
builder.Services.AddHttpClient<IContaCorrenteClient, ContaCorrenteClient>(client =>
{
    var baseUrl = builder.Configuration["ContaCorrenteApi:BaseUrl"] 
        ?? throw new InvalidOperationException("ContaCorrenteApi:BaseUrl não configurada");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Kafka
var kafkaConfig = new KafkaConfig();
builder.Configuration.GetSection("Kafka").Bind(kafkaConfig);
builder.Services.AddSingleton(kafkaConfig);
builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();

// Configurações
var tarifaConfig = new TarifaConfig();
builder.Configuration.GetSection("TarifaConfig").Bind(tarifaConfig);
builder.Services.AddSingleton(tarifaConfig);

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new EncryptedIdJsonConverter());
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Ailos Transferência API", Version = "v1" });
    
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

builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    await InitializeDatabase(app.Services);
}

app.UseHttpsRedirection();
app.MapHealthChecks("/health");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.UseMiddleware<IdempotenciaMiddleware>();

app.Run();

static async Task InitializeDatabase(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    
    using var connection = connectionFactory.CreateConnection();
    connection.Open();
    
    var sqlPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts/sql/transferencia.sql");
    
    if (File.Exists(sqlPath))
    {
        var sql = await File.ReadAllTextAsync(sqlPath);
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
        Console.WriteLine("Banco de dados de transferência inicializado com sucesso!");
    }
}
