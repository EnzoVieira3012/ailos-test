using Ailos.ContaCorrente.Api.Application.DTOs.Movimentacao.Request;
using Ailos.ContaCorrente.Api.Application.DTOs.Movimentacao.Response;

namespace Ailos.ContaCorrente.Api.Application.Services.Interfaces;

public interface IMovimentacaoService
{
    Task<MovimentacaoResponse> CriarMovimentacaoAsync(
        long contaIdUsuarioLogado,
        MovimentacaoRequest request,
        CancellationToken cancellationToken = default);
}
