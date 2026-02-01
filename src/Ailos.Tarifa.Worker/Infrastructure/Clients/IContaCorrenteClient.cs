using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Ailos.Tarifa.Worker.Infrastructure.Clients;

public interface IContaCorrenteClient
{
    Task<bool> AplicarTarifaAsync(
        long contaId, 
        long transferenciaId, 
        decimal valorTarifa, 
        CancellationToken cancellationToken = default);
}

public sealed class ContaCorrenteClient : IContaCorrenteClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ContaCorrenteClient> _logger;

    public ContaCorrenteClient(
        HttpClient httpClient, 
        ILogger<ContaCorrenteClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> AplicarTarifaAsync(
        long contaId, 
        long transferenciaId, 
        decimal valorTarifa, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                contaCorrenteId = contaId,
                transferenciaId,
                valor = valorTarifa,
                descricao = $"Tarifa de transferência #{transferenciaId}",
                identificacaoRequisicao = $"TARIFA-{transferenciaId}-{Guid.NewGuid():N}"
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/movimentacao", 
                request, 
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Tarifa aplicada com sucesso: Conta={ContaId}, Transferencia={TransferenciaId}, Valor={Valor}", 
                    contaId, transferenciaId, valorTarifa);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Erro ao aplicar tarifa: Status={StatusCode}, Response={Error}", 
                    response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar API de Conta Corrente para aplicar tarifa");
            return false;
        }
    }
}
