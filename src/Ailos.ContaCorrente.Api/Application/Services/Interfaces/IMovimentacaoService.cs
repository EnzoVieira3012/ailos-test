using Ailos.ContaCorrente.Api.Application.DTOs.Movimentacao;
using Ailos.ContaCorrente.Api.Domain.Entities;
using Ailos.ContaCorrente.Api.Infrastructure.Repositories;
using Ailos.EncryptedId;

namespace Ailos.ContaCorrente.Api.Application.Services.Interfaces;

public interface IMovimentacaoService
{
    Task<MovimentacaoResponse> CriarMovimentacaoAsync(
        long contaIdUsuarioLogado,
        MovimentacaoRequest request,
        CancellationToken cancellationToken = default);
}
