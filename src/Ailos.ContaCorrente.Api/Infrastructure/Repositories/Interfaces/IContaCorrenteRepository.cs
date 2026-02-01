using Ailos.ContaCorrente.Api.Domain.Entities;

namespace Ailos.ContaCorrente.Api.Infrastructure.Repositories.Interfaces;

public interface IContaCorrenteRepository
{
    Task<Conta?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Conta?> ObterPorNumeroAsync(int numero, CancellationToken cancellationToken = default);
    Task<Conta?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default);
    Task<Conta> InserirAsync(Conta conta, CancellationToken cancellationToken = default);
    Task AtualizarAsync(Conta conta, CancellationToken cancellationToken = default);
    Task<int> ObterProximoNumeroAsync(CancellationToken cancellationToken = default);
    Task<decimal> CalcularSaldoAsync(long contaId, CancellationToken cancellationToken = default);
}
