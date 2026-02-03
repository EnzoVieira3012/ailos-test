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
    private readonly ILogger<ContaCorrenteClient> _logger;

    public ContaCorrenteClient(
        HttpClient httpClient,
        IJwtTokenService jwtTokenService,
        ILogger<ContaCorrenteClient> logger)
    {
        _httpClient = httpClient;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task RealizarMovimentacaoAsync(
        long contaId,
        string tipoMovimento,
        decimal valor,
        string descricao,
        string identificacaoRequisicao,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // NORMALIZA O TIPO DE MOVIMENTO (CORREÇÃO PRINCIPAL)
            tipoMovimento = tipoMovimento?.Trim().ToUpper() switch
            {
                "C" or "CREDITO" => "C",
                "D" or "DEBITO"  => "D",
                _ => throw new ValidationException("Tipo de movimento inválido")
            };

            var token = _jwtTokenService.GenerateToken(contaId, "transferencia");

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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

            _logger.LogInformation(
                "Enviando movimentação: Conta={ContaId}, Tipo={Tipo}, Valor={Valor}",
                contaId, tipoMovimento, valor);

            var response = await _httpClient.PostAsync(
                "/api/movimentacao",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogError(
                    "Erro na movimentação: {StatusCode} - {Content}",
                    response.StatusCode,
                    errorContent);

                var error = JsonSerializer.Deserialize<ErrorResponse>(errorContent);

                var errorMessage = error?.Detail ?? "Erro na movimentação";
                var errorType = error?.Extensions?
                    .GetValueOrDefault("errorType")?.ToString()
                    ?? "MOVIMENTATION_ERROR";

                throw new ValidationException($"{errorMessage} ({errorType})");
            }

            _logger.LogInformation(
                "Movimentação realizada com sucesso para conta {ContaId}",
                contaId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Falha de comunicação com Conta Corrente API");
            throw new ValidationException(
                $"Falha na comunicação com serviço de conta corrente: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no ContaCorrenteClient");
            throw;
        }
    }

    private class ErrorResponse
    {
        public string? Detail { get; set; }
        public Dictionary<string, object>? Extensions { get; set; }
    }
}
