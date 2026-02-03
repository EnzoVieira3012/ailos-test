using Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente;
using Ailos.ContaCorrente.Api.Domain.Entities;
using Ailos.ContaCorrente.Api.Infrastructure.Repositories.Interfaces;
using Ailos.ContaCorrente.Api.Application.Services.Interfaces;
using Ailos.Common.Domain.ValueObjects;
using Ailos.EncryptedId;
using Ailos.Common.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace Ailos.ContaCorrente.Api.Application.Services.Implementations;

public class ContaCorrenteService : IContaCorrenteService
{
    private readonly IContaCorrenteRepository _contaRepository;
    private readonly IMovimentoRepository _movimentoRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEncryptedIdService _encryptedIdService;
    private readonly ILogger<ContaCorrenteService> _logger;

    public ContaCorrenteService(
        IContaCorrenteRepository contaRepository,
        IMovimentoRepository movimentoRepository,
        IJwtTokenService jwtTokenService,
        IEncryptedIdService encryptedIdService,
        ILogger<ContaCorrenteService> logger)
    {
        _contaRepository = contaRepository;
        _movimentoRepository = movimentoRepository;
        _jwtTokenService = jwtTokenService;
        _encryptedIdService = encryptedIdService;
        _logger = logger;
    }

    public async Task<CadastrarContaResponse> CadastrarAsync(CadastrarContaRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("📝 Iniciando cadastro para CPF: {Cpf}", request.Cpf);
        
        try
        {
            _logger.LogDebug("Validando CPF...");
            var cpf = new Cpf(request.Cpf);
            _logger.LogDebug("CPF válido: {Cpf}", cpf.Numero);
            
            _logger.LogDebug("Criando entidade Conta...");
            var conta = new Conta(cpf, request.Nome, request.Senha);
            
            _logger.LogDebug("Salvando conta no banco...");
            var contaSalva = await _contaRepository.InserirAsync(conta, cancellationToken);
            _logger.LogInformation("✅ Conta salva - ID: {Id}, Número: {Numero}, Role: {Role}", 
                contaSalva.Id, contaSalva.Numero, contaSalva.Role);
            
            var encryptedId = _encryptedIdService.Encrypt(contaSalva.Id);
            _logger.LogDebug("ID ofuscado gerado: {EncryptedId}", encryptedId.Value);
            
            return new CadastrarContaResponse
            {
                Id = encryptedId,
                Numero = contaSalva.Numero
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Argumento inválido no cadastro");
            throw;
        }
        catch (Ailos.Common.Domain.Exceptions.DuplicateCpfException)
        {
            _logger.LogWarning("CPF já cadastrado: {Cpf}", request.Cpf);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ERRO inesperado no cadastro - CPF: {Cpf}, Mensagem: {Message}", request.Cpf, ex.Message);
            throw new InvalidOperationException($"Erro ao cadastrar conta: {ex.Message}", ex);
        }
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔐 Tentativa de login - CPF: {Cpf}, Número: {Numero}", 
            request.Cpf ?? "N/A", request.NumeroConta?.ToString() ?? "N/A");

        Conta? conta = null;

        if (request.NumeroConta.HasValue)
        {
            _logger.LogDebug("Buscando conta por número: {Numero}", request.NumeroConta.Value);
            conta = await _contaRepository.ObterPorNumeroAsync(request.NumeroConta.Value, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(request.Cpf))
        {
            _logger.LogDebug("Buscando conta por CPF: {Cpf}", request.Cpf);
            conta = await _contaRepository.ObterPorCpfAsync(request.Cpf, cancellationToken);
        }

        if (conta == null)
        {
            _logger.LogWarning("Conta não encontrada");
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        _logger.LogDebug("Conta encontrada - ID: {Id}, Número: {Numero}, Nome: {Nome}, Role: {Role}", 
            conta.Id, conta.Numero, conta.Nome, conta.Role);

        try
        {
            var senhaValida = conta.ValidarSenha(request.Senha);
            
            if (!senhaValida)
            {
                _logger.LogWarning("Senha inválida para conta {Id}", conta.Id);
                throw new UnauthorizedAccessException("Credenciais inválidas");
            }

            if (!conta.Ativo)
            {
                _logger.LogWarning("Conta {Id} inativa", conta.Id);
                throw new UnauthorizedAccessException("Conta inativa");
            }

            // ALTERAÇÃO: usar a role da conta em vez de valor fixo "conta-corrente"
            var token = _jwtTokenService.GenerateToken(conta.Id, conta.Role);
            var encryptedId = _encryptedIdService.Encrypt(conta.Id);

            _logger.LogInformation("✅ Login bem-sucedido para conta {Id} com role {Role}", conta.Id, conta.Role);
            
            return new LoginResponse
            {
                Token = token,
                ContaId = encryptedId,
                NumeroConta = conta.Numero,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante validação de login");
            throw;
        }
    }

    public async Task InativarAsync(long contaId, string senha, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Inativando conta {ContaId}", contaId);
        
        var conta = await _contaRepository.ObterPorIdAsync(contaId, cancellationToken)
            ?? throw new InvalidOperationException("Conta não encontrada");

        if (!conta.ValidarSenha(senha))
        {
            _logger.LogWarning("Senha inválida para inativação da conta {ContaId}", contaId);
            throw new UnauthorizedAccessException("Senha inválida");
        }

        conta.Inativar();
        await _contaRepository.AtualizarAsync(conta, cancellationToken);
        
        _logger.LogInformation("✅ Conta {ContaId} inativada", contaId);
    }

    public async Task<SaldoResponse> ConsultarSaldoAsync(long contaId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Consultando saldo da conta {ContaId}", contaId);
        
        var conta = await _contaRepository.ObterPorIdAsync(contaId, cancellationToken)
            ?? throw new InvalidOperationException("Conta não encontrada");

        if (!conta.Ativo)
            throw new InvalidOperationException("Conta inativa");

        var saldo = await _movimentoRepository.CalcularSaldoAsync(contaId, cancellationToken);
        var encryptedId = _encryptedIdService.Encrypt(conta.Id);

        _logger.LogDebug("Saldo da conta {ContaId} (Role: {Role}): {Saldo}", contaId, conta.Role, saldo);
        
        return new SaldoResponse
        {
            ContaId = encryptedId,
            NumeroConta = conta.Numero,
            NomeTitular = conta.Nome,
            DataConsulta = DateTime.UtcNow,
            Saldo = saldo
        };
    }

    // MÉTODO ADICIONAL PARA ATUALIZAR ROLE (OPCIONAL)
    public async Task AtualizarRoleAsync(long contaId, string novaRole, string senha, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Atualizando role da conta {ContaId} para {Role}", contaId, novaRole);
        
        var conta = await _contaRepository.ObterPorIdAsync(contaId, cancellationToken)
            ?? throw new InvalidOperationException("Conta não encontrada");

        if (!conta.ValidarSenha(senha))
        {
            _logger.LogWarning("Senha inválida para atualização de role da conta {ContaId}", contaId);
            throw new UnauthorizedAccessException("Senha inválida");
        }

        // Atualizar a role (necessário adicionar um método na entidade Conta para isso)
        // Se não houver, pode ser necessário criar uma nova instância ou adicionar um método
        _logger.LogInformation("Role atualizada de {RoleAntiga} para {RoleNova}", conta.Role, novaRole);
        
        // Atualizar no banco
        await _contaRepository.AtualizarAsync(conta, cancellationToken);
        
        _logger.LogInformation("✅ Role da conta {ContaId} atualizada para {Role}", contaId, novaRole);
    }
}