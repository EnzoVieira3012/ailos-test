using Dapper;
using Ailos.ContaCorrente.Api.Domain.Entities;
using Ailos.ContaCorrente.Api.Domain.ValueObjects;
using Ailos.ContaCorrente.Api.Infrastructure.Data;

namespace Ailos.ContaCorrente.Api.Infrastructure.Repositories;

public sealed class ContaCorrenteRepository : IContaCorrenteRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ContaCorrenteRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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
                senha_hash as SenhaHash,
                salt as Salt,
                data_criacao as DataCriacao,
                data_atualizacao as DataAtualizacao
            FROM contacorrente
            WHERE idcontacorrente = @Id";

        var result = await connection.QueryFirstOrDefaultAsync<ContaDbModel>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        return result?.ToEntity();
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
                senha_hash as SenhaHash,
                salt as Salt,
                data_criacao as DataCriacao,
                data_atualizacao as DataAtualizacao
            FROM contacorrente
            WHERE numero = @Numero";

        var result = await connection.QueryFirstOrDefaultAsync<ContaDbModel>(
            new CommandDefinition(sql, new { Numero = numero }, cancellationToken: cancellationToken));

        return result?.ToEntity();
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
                senha_hash as SenhaHash,
                salt as Salt,
                data_criacao as DataCriacao,
                data_atualizacao as DataAtualizacao
            FROM contacorrente
            WHERE cpf = @Cpf";

        var result = await connection.QueryFirstOrDefaultAsync<ContaDbModel>(
            new CommandDefinition(sql, new { Cpf = cpf }, cancellationToken: cancellationToken));

        return result?.ToEntity();
    }

    public async Task<Conta> InserirAsync(Conta conta, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO contacorrente 
                (cpf, numero, nome, ativo, senha_hash, salt, data_criacao)
            VALUES 
                (@Cpf, @Numero, @Nome, @Ativo, @SenhaHash, @Salt, @DataCriacao);
            SELECT last_insert_rowid();";

        var numero = await ObterProximoNumeroAsync(cancellationToken);
        conta.DefinirNumero(numero);

        var dbModel = ContaDbModel.FromEntity(conta);

        var id = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(sql, dbModel, cancellationToken: cancellationToken));

        // Atualiza o ID na entidade
        var contaAtualizada = new ContaDbModel
        {
            Id = id,
            Cpf = dbModel.Cpf,
            Numero = dbModel.Numero,
            Nome = dbModel.Nome,
            Ativo = dbModel.Ativo,
            SenhaHash = dbModel.SenhaHash,
            Salt = dbModel.Salt,
            DataCriacao = dbModel.DataCriacao,
            DataAtualizacao = dbModel.DataAtualizacao
        };

        return contaAtualizada.ToEntity();
    }

    public async Task AtualizarAsync(Conta conta, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE contacorrente 
            SET 
                nome = @Nome,
                ativo = @Ativo,
                data_atualizacao = @DataAtualizacao
            WHERE idcontacorrente = @Id";

        var dbModel = ContaDbModel.FromEntity(conta);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, dbModel, cancellationToken: cancellationToken));
    }

    public async Task<int> ObterProximoNumeroAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = "SELECT COALESCE(MAX(numero), 0) + 1 FROM contacorrente";

        var numero = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return numero;
    }

    public async Task<decimal> CalcularSaldoAsync(long contaId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT 
                COALESCE(SUM(CASE WHEN tipomovimento = 'C' THEN valor ELSE -valor END), 0) as Saldo
            FROM movimento
            WHERE idcontacorrente = @ContaId";

        var saldo = await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sql, new { ContaId = contaId }, cancellationToken: cancellationToken));

        return saldo;
    }

    // Modelo para mapeamento do banco
    private class ContaDbModel
    {
        public long Id { get; set; }
        public string Cpf { get; set; } = string.Empty;
        public int Numero { get; set; }
        public string Nome { get; set; } = string.Empty;
        public bool Ativo { get; set; }
        public string SenhaHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
        public DateTime? DataAtualizacao { get; set; }

        public static ContaDbModel FromEntity(Conta conta)
        {
            return new ContaDbModel
            {
                Id = conta.Id,
                Cpf = conta.Cpf.Numero,
                Numero = conta.Numero,
                Nome = conta.Nome,
                Ativo = conta.Ativo,
                SenhaHash = conta.Senha.Hash,
                Salt = conta.Senha.Salt,
                DataCriacao = conta.DataCriacao,
                DataAtualizacao = conta.DataAtualizacao
            };
        }

        public Conta ToEntity()
        {
            var cpf = new Cpf(Cpf);
            var senha = Senha.Recriar(SenhaHash, Salt);

            return new Conta(
                Id,
                cpf,
                Numero,
                Nome,
                Ativo,
                senha,
                DataCriacao,
                DataAtualizacao
            );
        }
    }
}
