using Dapper;
using Ailos.ContaCorrente.Api.Domain.Entities;
using Ailos.ContaCorrente.Api.Infrastructure.Repositories.Interfaces;
using Ailos.Common.Domain.ValueObjects;
using Ailos.Common.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace Ailos.ContaCorrente.Api.Infrastructure.Repositories.Implementations;

public sealed class ContaCorrenteRepository : IContaCorrenteRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<ContaCorrenteRepository> _logger;

    public ContaCorrenteRepository(IDbConnectionFactory connectionFactory, ILogger<ContaCorrenteRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Conta?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT 
                idcontacorrente as Id,
                cpf as Cpf,
                numero as Numero,
                nome as Nome,
                ativo as Ativo,
                senha_hash as Hash,
                data_criacao as DataCriacao,
                data_atualizacao as DataAtualizacao,
                role as Role  -- COLUNA ROLE ADICIONADA
            FROM contacorrente
            WHERE idcontacorrente = @Id";

        try
        {
            var result = await connection.QueryFirstOrDefaultAsync<ContaDbModel>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

            return result?.ToEntity();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter conta por ID {Id}", id);
            throw;
        }
    }

    public async Task<Conta?> ObterPorNumeroAsync(int numero, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT 
                idcontacorrente as Id,
                cpf as Cpf,
                numero as Numero,
                nome as Nome,
                ativo as Ativo,
                senha_hash as Hash,
                data_criacao as DataCriacao,
                data_atualizacao as DataAtualizacao,
                role as Role  -- COLUNA ROLE ADICIONADA
            FROM contacorrente
            WHERE numero = @Numero";

        try
        {
            var result = await connection.QueryFirstOrDefaultAsync<ContaDbModel>(
                new CommandDefinition(sql, new { Numero = numero }, cancellationToken: cancellationToken));

            return result?.ToEntity();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter conta por número {Numero}", numero);
            throw;
        }
    }

    public async Task<Conta?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT 
                idcontacorrente as Id,
                cpf as Cpf,
                numero as Numero,
                nome as Nome,
                ativo as Ativo,
                senha_hash as Hash,
                data_criacao as DataCriacao,
                data_atualizacao as DataAtualizacao,
                role as Role  -- COLUNA ROLE ADICIONADA
            FROM contacorrente
            WHERE cpf = @Cpf";

        try
        {
            var result = await connection.QueryFirstOrDefaultAsync<ContaDbModel>(
                new CommandDefinition(sql, new { Cpf = cpf }, cancellationToken: cancellationToken));

            return result?.ToEntity();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter conta por CPF {Cpf}", cpf);
            throw;
        }
    }

    public async Task<Conta> InserirAsync(Conta conta, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO contacorrente 
                (cpf, numero, nome, ativo, senha_hash, data_criacao, role)
            VALUES 
                (@Cpf, @Numero, @Nome, @Ativo, @Hash, @DataCriacao, @Role);
            SELECT last_insert_rowid();";

        var numero = await ObterProximoNumeroAsync(cancellationToken);
        conta.DefinirNumero(numero);

        var dbModel = ContaDbModel.FromEntity(conta);

        try
        {
            var id = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(sql, dbModel, cancellationToken: cancellationToken));

            var contaAtualizada = new ContaDbModel
            {
                Id = id,
                Cpf = dbModel.Cpf,
                Numero = dbModel.Numero,
                Nome = dbModel.Nome,
                Ativo = dbModel.Ativo,
                Hash = dbModel.Hash,
                DataCriacao = dbModel.DataCriacao,
                DataAtualizacao = dbModel.DataAtualizacao,
                Role = dbModel.Role
            };

            return contaAtualizada.ToEntity();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            _logger.LogError(ex, "Violação de constraint ao inserir conta");
            
            if (ex.Message.Contains("cpf", StringComparison.OrdinalIgnoreCase))
            {
                throw new Ailos.Common.Domain.Exceptions.DuplicateCpfException();
            }
            else if (ex.Message.Contains("numero", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Número de conta já existe");
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inserir conta");
            throw;
        }
    }

    public async Task AtualizarAsync(Conta conta, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE contacorrente 
            SET 
                nome = @Nome,
                ativo = @Ativo,
                data_atualizacao = @DataAtualizacao,
                role = @Role  -- ROLE ADICIONADA
            WHERE idcontacorrente = @Id";

        var dbModel = ContaDbModel.FromEntity(conta);

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(sql, dbModel, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar conta {Id}", conta.Id);
            throw;
        }
    }

    public async Task<int> ObterProximoNumeroAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = "SELECT COALESCE(MAX(numero), 0) + 1 FROM contacorrente";

        try
        {
            var numero = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, cancellationToken: cancellationToken));

            return numero;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter próximo número");
            throw;
        }
    }

    public async Task<decimal> CalcularSaldoAsync(long contaId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT 
                COALESCE(SUM(CASE WHEN tipomovimento = 'C' THEN valor ELSE -valor END), 0) as Saldo
            FROM movimento
            WHERE idcontacorrente = @ContaId";

        try
        {
            var saldo = await connection.ExecuteScalarAsync<decimal>(
                new CommandDefinition(sql, new { ContaId = contaId }, cancellationToken: cancellationToken));

            return saldo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular saldo da conta {ContaId}", contaId);
            throw;
        }
    }

    private class ContaDbModel
    {
        public long Id { get; set; }
        public string Cpf { get; set; } = string.Empty;
        public int Numero { get; set; }
        public string Nome { get; set; } = string.Empty;
        public bool Ativo { get; set; }
        public string Hash { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public string Role { get; set; } = "conta-corrente";

        public static ContaDbModel FromEntity(Conta conta)
        {
            return new ContaDbModel
            {
                Id = conta.Id,
                Cpf = conta.Cpf.Numero,
                Numero = conta.Numero,
                Nome = conta.Nome,
                Ativo = conta.Ativo,
                Hash = conta.Senha.Hash,
                DataCriacao = conta.DataCriacao,
                DataAtualizacao = conta.DataAtualizacao,
                Role = conta.Role
            };
        }

        public Conta ToEntity()
        {
            var cpf = new Cpf(Cpf);
            var senha = Senha.Recriar(Hash);

            return new Conta(
                Id,
                cpf,
                Numero,
                Nome,
                Ativo,
                senha,
                DataCriacao,
                DataAtualizacao,
                Role
            );
        }
    }
}
