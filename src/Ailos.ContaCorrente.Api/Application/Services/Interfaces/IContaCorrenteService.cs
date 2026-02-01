using Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente;

namespace Ailos.ContaCorrente.Api.Application.Services.Interfaces;

public interface IContaCorrenteService
{
    Task<CadastrarContaResponse> CadastrarAsync(CadastrarContaRequest request, CancellationToken cancellationToken);
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task InativarAsync(long contaId, string senha, CancellationToken cancellationToken);
    Task<SaldoResponse> ConsultarSaldoAsync(long contaId, CancellationToken cancellationToken);
}
