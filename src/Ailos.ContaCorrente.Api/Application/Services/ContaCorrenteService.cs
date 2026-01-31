// src/Ailos.ContaCorrente.Api/Application/Services/ContaCorrenteService.cs
using Ailos.ContaCorrente.Api.Application.DTOs.ContaCorrente;
using Ailos.ContaCorrente.Api.Domain.Entities;
using Ailos.ContaCorrente.Api.Domain.ValueObjects;
using Ailos.ContaCorrente.Api.Infrastructure.Repositories;
using Ailos.ContaCorrente.Api.Infrastructure.Security;
using Ailos.EncryptedId;

namespace Ailos.ContaCorrente.Api.Application.Services;

public class ContaCorrenteService : IContaCorrenteService
{
    private readonly IContaCorrenteRepository _contaRepository;
    private readonly IMovimentoRepository _movimentoRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEncryptedIdService _encryptedIdService;

    public ContaCorrenteService(
        IContaCorrenteRepository contaRepository,
        IMovimentoRepository movimentoRepository,
        IJwtTokenService jwtTokenService,
        IEncryptedIdService encryptedIdService)
    {
        _contaRepository = contaRepository;
        _movimentoRepository = movimentoRepository;
        _jwtTokenService = jwtTokenService;
        _encryptedIdService = encryptedIdService;
    }

    public async Task<CadastrarContaResponse> CadastrarAsync(CadastrarContaRequest request, CancellationToken cancellationToken)
    {
        var cpf = new Cpf(request.Cpf);
        var conta = new Conta(cpf, request.Nome, request.Senha);
        
        var contaSalva = await _contaRepository.InserirAsync(conta, cancellationToken);
        var encryptedId = _encryptedIdService.Encrypt(contaSalva.Id);
        
        return new CadastrarContaResponse
        {
            Id = encryptedId,
            Numero = contaSalva.Numero
        };
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        Conta? conta = null;
        
        if (request.NumeroConta.HasValue)
        {
            conta = await _contaRepository.ObterPorNumeroAsync(request.NumeroConta.Value, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(request.Cpf))
        {
            conta = await _contaRepository.ObterPorCpfAsync(request.Cpf, cancellationToken);
        }
        
        if (conta == null || !conta.ValidarSenha(request.Senha) || !conta.Ativo)
        {
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }
        
        var token = _jwtTokenService.GenerateToken(conta.Id, conta.Numero);
        var encryptedId = _encryptedIdService.Encrypt(conta.Id);
        
        return new LoginResponse
        {
            Token = token,
            ContaId = encryptedId,
            NumeroConta = conta.Numero
        };
    }

    public async Task InativarAsync(long contaId, string senha, CancellationToken cancellationToken)
    {
        var conta = await _contaRepository.ObterPorIdAsync(contaId, cancellationToken)
            ?? throw new InvalidOperationException("Conta não encontrada");
        
        if (!conta.ValidarSenha(senha))
            throw new UnauthorizedAccessException("Senha inválida");
        
        conta.Inativar();
        await _contaRepository.AtualizarAsync(conta, cancellationToken);
    }

    public async Task<SaldoResponse> ConsultarSaldoAsync(long contaId, CancellationToken cancellationToken)
    {
        var conta = await _contaRepository.ObterPorIdAsync(contaId, cancellationToken)
            ?? throw new InvalidOperationException("Conta não encontrada");
        
        if (!conta.Ativo)
            throw new InvalidOperationException("Conta inativa");
        
        var saldo = await _movimentoRepository.CalcularSaldoAsync(contaId, cancellationToken);
        var encryptedId = _encryptedIdService.Encrypt(conta.Id);
        
        return new SaldoResponse
        {
            ContaId = encryptedId,
            NumeroConta = conta.Numero,
            NomeTitular = conta.Nome,
            DataConsulta = DateTime.UtcNow,
            Saldo = saldo
        };
    }
}
