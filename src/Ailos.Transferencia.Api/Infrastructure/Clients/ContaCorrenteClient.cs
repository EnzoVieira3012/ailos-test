using System.Text;
using System.Text.Json;
using Ailos.Common.Domain.Exceptions;
using Ailos.Common.Infrastructure.Security;

namespace Ailos.Transferencia.Api.Infrastructure.Clients;

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

public sealed class ContaCorrenteClient : IContaCorrenteClient
{
    private readonly HttpClient _httpClient;
    private readonly IJwtTokenService _jwtTokenService;

    public ContaCorrenteClient(
        HttpClient httpClient,
        IJwtTokenService jwtTokenService)
    {
        _httpClient = httpClient;
        _jwtTokenService = jwtTokenService;
    }

    public async Task RealizarMovimentacaoAsync(
        long contaId,
        string tipoMovimento,
        decimal valor,
        string descricao,
        string identificacaoRequisicao,
        CancellationToken cancellationToken = default)
    {
        var token = _jwtTokenService.GenerateToken(contaId, "transferencia");
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            identificacaoRequisicao,
            contaCorrenteId = contaId,
            valor,
            tipoMovimento,
            descricao
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/api/movimentacao", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var error = JsonSerializer.Deserialize<ErrorResponse>(errorContent);

            var errorMessage = error?.Detail ?? "Erro na movimentação";
            var errorType = error?.Extensions?.GetValueOrDefault("errorType")?.ToString() ?? "MOVIMENTATION_ERROR";
            
            // Usar ValidationException do Common em vez de DomainException abstrato
            throw new ValidationException($"{errorMessage} ({errorType})");
        }
    }

    private class ErrorResponse
    {
        public string? Detail { get; set; }
        public Dictionary<string, object>? Extensions { get; set; }
    }
}
