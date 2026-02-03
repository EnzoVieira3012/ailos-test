using System.Net.Http.Json;
using Ailos.Common.Infrastructure.Security;
using Ailos.EncryptedId;

namespace Ailos.Tarifa.Worker.Infrastructure.Clients.Interfaces;

public sealed class ContaCorrenteClient : IContaCorrenteClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ContaCorrenteClient> _logger;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEncryptedIdService _encryptedIdService;

    public ContaCorrenteClient(
        HttpClient httpClient, 
        ILogger<ContaCorrenteClient> logger,
        IJwtTokenService jwtTokenService,
        IEncryptedIdService encryptedIdService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jwtTokenService = jwtTokenService;
        _encryptedIdService = encryptedIdService;
    }

    public async Task<bool> AplicarTarifaAsync(
        long contaId, 
        long transferenciaId, 
        decimal valorTarifa, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var token = _jwtTokenService.GenerateToken(contaId, "tarifa-worker");
            
            var contaIdEncrypted = _encryptedIdService.Encrypt(contaId);
            
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            
            _logger.LogInformation("🔐 Gerando token JWT para conta {ContaId} (Encrypted: {EncryptedId})", 
                contaId, contaIdEncrypted.Value);

            var request = new
            {
                contaCorrenteId = contaIdEncrypted.Value,
                transferenciaId,
                valor = valorTarifa,
                descricao = $"Tarifa de transferência #{transferenciaId}",
                identificacaoRequisicao = $"TARIFA-{transferenciaId}-{Guid.NewGuid():N}",
                tipoMovimento = "D"
            };

            _logger.LogDebug("📤 Enviando tarifa para API: Conta={ContaId} (Encrypted: {EncryptedId}), Valor={Valor}", 
                contaId, contaIdEncrypted.Value, valorTarifa);

            var response = await _httpClient.PostAsJsonAsync(
                "/api/movimentacao", 
                request, 
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Tarifa aplicada com sucesso: Conta={ContaId}, Transferencia={TransferenciaId}, Valor={Valor}", 
                    contaId, transferenciaId, valorTarifa);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("❌ Erro ao aplicar tarifa: Status={StatusCode}, Response={Error}", 
                    response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Falha ao chamar API de Conta Corrente para aplicar tarifa");
            return false;
        }
    }
}
