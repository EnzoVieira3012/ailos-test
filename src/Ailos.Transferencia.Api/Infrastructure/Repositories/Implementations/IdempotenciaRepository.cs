using Dapper;
using Ailos.Transferencia.Api.Domain.Entities;
using Ailos.Transferencia.Api.Infrastructure.Repositories;
using Ailos.Common.Infrastructure.Data;

namespace Ailos.Transferencia.Api.Infrastructure.Repositories.Implementations;

public sealed class IdempotenciaRepository : IIdempotenciaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public IdempotenciaRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Idempotencia?> ObterPorChaveAsync(string chave, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT 
                chave_idempotencia as Chave,
                requisicao as Requisicao,
                resultado as Resultado,
                data_criacao as DataCriacao
            FROM idempotencia
            WHERE chave_idempotencia = @Chave";

        var result = await connection.QueryFirstOrDefaultAsync<IdempotenciaDbModel>(
            new CommandDefinition(sql, new { Chave = chave }, cancellationToken: cancellationToken));

        return result?.ToEntity();
    }

    public async Task RegistrarAsync(string chave, string? requisicao, string? resultado, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT OR REPLACE INTO idempotencia 
                (chave_idempotencia, requisicao, resultado, data_criacao)
            VALUES 
                (@Chave, @Requisicao, @Resultado, @DataCriacao)";

        var dbModel = new IdempotenciaDbModel
        {
            Chave = chave,
            Requisicao = requisicao,
            Resultado = resultado,
            DataCriacao = DateTime.UtcNow
        };

        await connection.ExecuteAsync(
            new CommandDefinition(sql, dbModel, cancellationToken: cancellationToken));
    }

    // Modelo para mapeamento do banco
    private class IdempotenciaDbModel
    {
        public string Chave { get; set; } = string.Empty;
        public string? Requisicao { get; set; }
        public string? Resultado { get; set; }
        public DateTime DataCriacao { get; set; }

        public Idempotencia ToEntity()
        {
            return new Idempotencia(Chave, Requisicao, Resultado, DataCriacao);
        }
    }
}
