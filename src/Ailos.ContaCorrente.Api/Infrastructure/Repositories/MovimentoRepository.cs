using Dapper;
using Ailos.ContaCorrente.Api.Domain.Entities;
using Ailos.ContaCorrente.Api.Infrastructure.Data;

namespace Ailos.ContaCorrente.Api.Infrastructure.Repositories;

public sealed class MovimentoRepository : IMovimentoRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MovimentoRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Movimento> InserirAsync(Movimento movimento, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO movimento 
                (idcontacorrente, datamovimento, tipomovimento, valor, descricao)
            VALUES 
                (@ContaCorrenteId, @DataMovimento, @TipoMovimento, @Valor, @Descricao);
            SELECT last_insert_rowid();";

        var dbModel = MovimentoDbModel.FromEntity(movimento);

        var id = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, dbModel, cancellationToken: cancellationToken));

        // Criar um novo movimento com o ID
        return new Movimento(dbModel.ContaCorrenteId, dbModel.TipoMovimento, dbModel.Valor, dbModel.Descricao)
        {
            Id = id,
            DataMovimento = dbModel.DataMovimento
        };
    }

    public async Task<IEnumerable<Movimento>> ObterPorContaAsync(
        long contaId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT 
                idmovimento as Id,
                idcontacorrente as ContaCorrenteId,
                datamovimento as DataMovimento,
                tipomovimento as TipoMovimento,
                valor as Valor,
                descricao as Descricao
            FROM movimento
            WHERE idcontacorrente = @ContaId
            ORDER BY datamovimento DESC";

        var results = await connection.QueryAsync<MovimentoDbModel>(
            new CommandDefinition(sql, new { ContaId = contaId }, cancellationToken: cancellationToken));

        return results.Select(r => r.ToEntity());
    }

    public async Task<decimal> CalcularSaldoAsync(long contaId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT 
                COALESCE(SUM(CASE WHEN tipomovimento = 'C' THEN valor ELSE -valor END), 0) as Saldo
            FROM movimento
            WHERE idcontacorrente = @ContaId";

        var saldo = await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sql, new { ContaId = contaId }, cancellationToken: cancellationToken));

        return saldo;
    }

    // Modelo para mapeamento do banco
    private class MovimentoDbModel
    {
        public long Id { get; set; }
        public long ContaCorrenteId { get; set; }
        public DateTime DataMovimento { get; set; }
        public char TipoMovimento { get; set; }
        public decimal Valor { get; set; }
        public string? Descricao { get; set; }

        public static MovimentoDbModel FromEntity(Movimento movimento)
        {
            return new MovimentoDbModel
            {
                Id = movimento.Id,
                ContaCorrenteId = movimento.ContaCorrenteId,
                DataMovimento = movimento.DataMovimento,
                TipoMovimento = movimento.TipoMovimento,
                Valor = movimento.Valor,
                Descricao = movimento.Descricao
            };
        }

        public Movimento ToEntity()
        {
            var movimento = new Movimento(ContaCorrenteId, TipoMovimento, Valor, Descricao)
            {
                Id = Id,
                DataMovimento = DataMovimento
            };
            
            return movimento;
        }
    }
}
