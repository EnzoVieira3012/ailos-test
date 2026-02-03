using System.Text;
using System.Text.Json;
using Ailos.Common.Domain.Exceptions;
using Ailos.Common.Infrastructure.Security;
using Ailos.EncryptedId;

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
    private readonly IEncryptedIdService _encryptedIdService;
    private readonly ILogger<ContaCorrenteClient> _logger;

    public ContaCorrenteClient(
        HttpClient httpClient,
        IJwtTokenService jwtTokenService,
        IEncryptedIdService encryptedIdService,
        ILogger<ContaCorrenteClient> logger)
    {
        _httpClient = httpClient;
        _jwtTokenService = jwtTokenService;
        _encryptedIdService = encryptedIdService;
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
        HttpResponseMessage response = null!;
        
        try
        {
            // NORMALIZA O TIPO DE MOVIMENTO
            tipoMovimento = tipoMovimento?.Trim().ToUpper() switch
            {
                "C" or "CREDITO" or "CRÉDITO" => "C",
                "D" or "DEBITO" or "DÉBITO"  => "D",
                _ => throw new ValidationException("Tipo de movimento inválido")
            };

            var token = _jwtTokenService.GenerateToken(contaId, "transferencia");

            // 🔥 CORREÇÃO CRÍTICA: GERAR ENCRYPTEDID
            var encryptedId = _encryptedIdService.Encrypt(contaId);

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new
            {
                identificacaoRequisicao,
                contaCorrenteId = encryptedId.Value,
                valor,
                tipoMovimento,
                descricao
            };

            _logger.LogDebug("📤 ENVIANDO PARA CONTA CORRENTE API:");
            _logger.LogDebug("   URL: {BaseUrl}/api/movimentacao", _httpClient.BaseAddress);
            _logger.LogDebug("   Conta ID (numérico): {ContaId}", contaId);
            _logger.LogDebug("   Conta ID (encriptado): {EncryptedId}", encryptedId.Value);
            _logger.LogDebug("   Tipo Movimento: {TipoMovimento}", tipoMovimento);
            _logger.LogDebug("   Valor: {Valor}", valor);
            _logger.LogDebug("   Descrição: {Descricao}", descricao);
            _logger.LogDebug("   ID Requisição: {IdRequisicao}", identificacaoRequisicao);
            _logger.LogDebug("   Token JWT: {TokenLength} chars", token.Length);

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            // Log do request body completo
            var requestBody = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogTrace("📦 REQUEST BODY:\n{RequestBody}", requestBody);

            _logger.LogInformation("🚀 Enviando movimentação para Conta Corrente API...");
            
            response = await _httpClient.PostAsync(
                "/api/movimentacao",
                content,
                cancellationToken);

            _logger.LogDebug("📥 RESPOSTA RECEBIDA: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
                _logger.LogError("❌ ERRO NA CONTA CORRENTE API:");
                _logger.LogError("   Status Code: {StatusCode}", response.StatusCode);
                _logger.LogError("   Content Type: {ContentType}", response.Content.Headers.ContentType);
                _logger.LogError("   Error Content: {ErrorContent}", errorContent);

                try
                {
                    var error = JsonSerializer.Deserialize<ErrorResponse>(errorContent);

                    var errorMessage = error?.Detail ?? "Erro na movimentação";
                    var errorType = error?.Extensions?
                        .GetValueOrDefault("errorType")?.ToString()
                        ?? "MOVIMENTATION_ERROR";

                    _logger.LogError("   Error Message: {ErrorMessage}", errorMessage);
                    _logger.LogError("   Error Type: {ErrorType}", errorType);

                    throw new ValidationException($"{errorMessage} ({errorType})");
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "   ❌ Não foi possível desserializar erro");
                    throw new ValidationException($"Erro na movimentação: {errorContent}");
                }
            }

            _logger.LogInformation("✅ Movimentação realizada com sucesso para conta {ContaId}", contaId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ FALHA DE COMUNICAÇÃO HTTP com Conta Corrente API");
            
            // Tenta ler mais detalhes da resposta se existir
            if (response != null)
            {
                try
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("   Conteúdo do erro: {ErrorContent}", errorContent);
                }
                catch (Exception readEx)
                {
                    _logger.LogError(readEx, "   Não foi possível ler conteúdo do erro");
                }
            }
            
            throw new ValidationException($"Falha na comunicação com serviço de conta corrente: {ex.Message}");
        }
        catch (ValidationException)
        {
            throw; // Re-lança ValidationException sem modificar
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ERRO INESPERADO no ContaCorrenteClient");
            throw;
        }
    }

    private class ErrorResponse
    {
        public string? Detail { get; set; }
        public Dictionary<string, object>? Extensions { get; set; }
    }
}
