namespace Ailos.Transferencia.Api.Infrastructure.Clients.Interfaces;

public interface IContaCorrenteClient
{
    Task RealizarMovimentacaoAsync(
        long contaId,
        string tipoMovimento,
        decimal valor,
        string descricao,
        string identificacaoRequisicao,
        CancellationToken cancellationToken = default);
}
