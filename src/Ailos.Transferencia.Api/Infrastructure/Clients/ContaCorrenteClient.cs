using System.Text;
using System.Text.Json;
using Ailos.Transferencia.Api.Domain.Exceptions;
using EncryptedIdType = Ailos.EncryptedId.EncryptedId;
using Ailos.Transferencia.Api.Infrastructure.Security;

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
        var token = _jwtTokenService.GenerateToken();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var encryptedContaId = new EncryptedIdType("placeholder"); // Será convertido pelo JsonConverter
        // Na prática, precisamos converter para EncryptedId usando o serviço
        // Mas isso será feito na serialização automática
        
        var request = new
        {
            identificacaoRequisicao,
            contaCorrenteId = contaId, // O JsonConverter vai converter automaticamente
            valor,
            tipoMovimento,
            descricao
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                Converters = { new Ailos.EncryptedId.JsonConverters.EncryptedIdJsonConverter() }
            }),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/api/movimentacao", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var error = JsonSerializer.Deserialize<ErrorResponse>(errorContent);

            throw response.StatusCode switch
            {
                System.Net.HttpStatusCode.BadRequest => new DomainException(
                    error?.Detail ?? "Erro na movimentação",
                    error?.Extensions?.GetValueOrDefault("errorType")?.ToString() ?? "MOVIMENTATION_ERROR"),
                
                System.Net.HttpStatusCode.Unauthorized => new UnauthorizedAccessException("Token inválido"),
                
                System.Net.HttpStatusCode.Forbidden => new UnauthorizedAccessException("Acesso negado"),
                
                _ => new InvalidOperationException($"Falha na chamada à API: {response.StatusCode}")
            };
        }
    }

    private class ErrorResponse
    {
        public string? Detail { get; set; }
        public Dictionary<string, object>? Extensions { get; set; }
    }
}
