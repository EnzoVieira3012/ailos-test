using Dapper;
using Ailos.Tarifa.Worker.Domain.Entities;
using Ailos.Common.Infrastructure.Data;

namespace Ailos.Tarifa.Worker.Infrastructure.Repositories.Implementations;

public sealed class TarifaRepository : ITarifaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TarifaRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> InserirTarifaAsync(TarifaEntity tarifa, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO tarifa 
                (idcontacorrente, idtransferencia, datamovimento, valor, processada, mensagem_erro, data_processamento)
            VALUES 
                (@ContaCorrenteId, @TransferenciaId, @DataMovimento, @Valor, @Processada, @MensagemErro, @DataProcessamento);
            SELECT last_insert_rowid();";

        var parameters = new
        {
            tarifa.ContaCorrenteId,
            tarifa.TransferenciaId,
            tarifa.DataMovimento,
            tarifa.Valor,
            Processada = tarifa.Processada ? 1 : 0,
            tarifa.MensagemErro,
            tarifa.DataProcessamento
        };

        var id = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        return id;
    }

    public async Task RegistrarProcessamentoAsync(TarifaProcessadaEntity historico, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO tarifa_processada 
                (transferencia_id, conta_origem_id, valor_tarifa, data_processamento, 
                 status, mensagem, topico_kafka, offset_kafka)
            VALUES 
                (@TransferenciaId, @ContaOrigemId, @ValorTarifa, @DataProcessamento, 
                 @Status, @Mensagem, @TopicoKafka, @OffsetKafka)";

        var parameters = new
        {
            historico.TransferenciaId,
            historico.ContaOrigemId,
            historico.ValorTarifa,
            historico.DataProcessamento,
            historico.Status,
            historico.Mensagem,
            historico.TopicoKafka,
            historico.OffsetKafka
        };

        await connection.ExecuteAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<bool> TransferenciaJaProcessadaAsync(
        long transferenciaId, 
        string topico, 
        long offset, 
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT COUNT(1) 
            FROM tarifa_processada 
            WHERE transferencia_id = @TransferenciaId 
              AND topico_kafka = @Topico 
              AND offset_kafka = @Offset";

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, 
                new { TransferenciaId = transferenciaId, Topico = topico, Offset = offset }, 
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<IEnumerable<TarifaProcessadaEntity>> ObterHistoricoAsync(
        DateTime dataInicio, 
        DateTime dataFim, 
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT 
                id as Id,
                transferencia_id as TransferenciaId,
                conta_origem_id as ContaOrigemId,
                valor_tarifa as ValorTarifa,
                data_processamento as DataProcessamento,
                status as Status,
                mensagem as Mensagem,
                topico_kafka as TopicoKafka,
                offset_kafka as OffsetKafka
            FROM tarifa_processada
            WHERE data_processamento BETWEEN @DataInicio AND @DataFim
            ORDER BY data_processamento DESC";

        var results = await connection.QueryAsync<TarifaProcessadaEntity>(
            new CommandDefinition(sql, 
                new { DataInicio = dataInicio, DataFim = dataFim }, 
                cancellationToken: cancellationToken));

        return results;
    }
}
