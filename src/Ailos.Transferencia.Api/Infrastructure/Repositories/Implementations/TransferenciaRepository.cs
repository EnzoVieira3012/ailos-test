using Dapper;
using Ailos.Transferencia.Api.Domain.Entities;
using Ailos.Transferencia.Api.Infrastructure.Repositories;
using Ailos.Common.Infrastructure.Data;

namespace Ailos.Transferencia.Api.Infrastructure.Repositories.Implementations;

public sealed class TransferenciaRepository : ITransferenciaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TransferenciaRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<TransferenciaEntity?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT 
                idtransferencia as Id,
                idcontacorrente_origem as ContaCorrenteOrigemId,
                idcontacorrente_destino as ContaCorrenteDestinoId,
                datamovimento as DataMovimento,
                valor as Valor,
                tarifa_aplicada as TarifaAplicada,
                status as Status,
                mensagem_erro as MensagemErro,
                identificacao_requisicao as IdentificacaoRequisicao
            FROM transferencia
            WHERE idtransferencia = @Id";

        var result = await connection.QueryFirstOrDefaultAsync<TransferenciaDbModel>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        return result?.ToEntity();
    }

    public async Task<TransferenciaEntity> InserirAsync(
        TransferenciaEntity transferencia, 
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO transferencia 
                (idcontacorrente_origem, idcontacorrente_destino, datamovimento, 
                 valor, tarifa_aplicada, status, mensagem_erro, identificacao_requisicao)
            VALUES 
                (@ContaCorrenteOrigemId, @ContaCorrenteDestinoId, @DataMovimento, 
                 @Valor, @TarifaAplicada, @Status, @MensagemErro, @IdentificacaoRequisicao);
            SELECT last_insert_rowid();";

        var dbModel = TransferenciaDbModel.FromEntity(transferencia);

        var id = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, dbModel, cancellationToken: cancellationToken));

        // Usar o novo construtor completo
        var transferenciaAtualizada = new TransferenciaEntity(
            id: id,
            contaCorrenteOrigemId: dbModel.ContaCorrenteOrigemId,
            contaCorrenteDestinoId: dbModel.ContaCorrenteDestinoId,
            dataMovimento: dbModel.DataMovimento,
            valor: dbModel.Valor,
            tarifaAplicada: dbModel.TarifaAplicada,
            status: Enum.Parse<TransferenciaStatus>(dbModel.Status),
            mensagemErro: dbModel.MensagemErro,
            identificacaoRequisicao: dbModel.IdentificacaoRequisicao);

        return transferenciaAtualizada;
    }

    public async Task AtualizarAsync(
        TransferenciaEntity transferencia, 
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE transferencia 
            SET 
                tarifa_aplicada = @TarifaAplicada,
                status = @Status,
                mensagem_erro = @MensagemErro
            WHERE idtransferencia = @Id";

        var dbModel = TransferenciaDbModel.FromEntity(transferencia);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, dbModel, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<TransferenciaEntity>> ObterPorContaAsync(
        long contaId, 
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT 
                idtransferencia as Id,
                idcontacorrente_origem as ContaCorrenteOrigemId,
                idcontacorrente_destino as ContaCorrenteDestinoId,
                datamovimento as DataMovimento,
                valor as Valor,
                tarifa_aplicada as TarifaAplicada,
                status as Status,
                mensagem_erro as MensagemErro,
                identificacao_requisicao as IdentificacaoRequisicao
            FROM transferencia
            WHERE idcontacorrente_origem = @ContaId OR idcontacorrente_destino = @ContaId
            ORDER BY datamovimento DESC";

        var results = await connection.QueryAsync<TransferenciaDbModel>(
            new CommandDefinition(sql, new { ContaId = contaId }, cancellationToken: cancellationToken));

        return results.Select(r => r.ToEntity());
    }

    // Modelo para mapeamento do banco
    private class TransferenciaDbModel
    {
        public long Id { get; set; }
        public long ContaCorrenteOrigemId { get; set; }
        public long ContaCorrenteDestinoId { get; set; }
        public DateTime DataMovimento { get; set; }
        public decimal Valor { get; set; }
        public decimal? TarifaAplicada { get; set; }
        public string Status { get; set; } = "PROCESSANDO";
        public string? MensagemErro { get; set; }
        public string? IdentificacaoRequisicao { get; set; }

        public static TransferenciaDbModel FromEntity(TransferenciaEntity entity)
        {
            return new TransferenciaDbModel
            {
                Id = entity.Id,
                ContaCorrenteOrigemId = entity.ContaCorrenteOrigemId,
                ContaCorrenteDestinoId = entity.ContaCorrenteDestinoId,
                DataMovimento = entity.DataMovimento,
                Valor = entity.Valor,
                TarifaAplicada = entity.TarifaAplicada,
                Status = entity.Status.ToString(),
                MensagemErro = entity.MensagemErro,
                IdentificacaoRequisicao = entity.IdentificacaoRequisicao
            };
        }

        public TransferenciaEntity ToEntity()
        {
            return new TransferenciaEntity(
                id: Id,
                contaCorrenteOrigemId: ContaCorrenteOrigemId,
                contaCorrenteDestinoId: ContaCorrenteDestinoId,
                dataMovimento: DataMovimento,
                valor: Valor,
                tarifaAplicada: TarifaAplicada,
                status: Enum.Parse<TransferenciaStatus>(Status),
                mensagemErro: MensagemErro,
                identificacaoRequisicao: IdentificacaoRequisicao);
        }
    }
}
