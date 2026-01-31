namespace Ailos.ContaCorrente.Api.Domain.ValueObjects;

public sealed record Senha
{
    public string Hash { get; }
    public string Salt { get; }

    private Senha(string hash, string salt)
    {
        Hash = hash;
        Salt = salt;
    }

    public static Senha Criar(string senhaTexto)
    {
        if (string.IsNullOrWhiteSpace(senhaTexto) || senhaTexto.Length < 6)
            throw new ArgumentException("Senha deve ter no mínimo 6 caracteres");

        var salt = BCrypt.Net.BCrypt.GenerateSalt(12);
        var hash = BCrypt.Net.BCrypt.HashPassword(senhaTexto, salt);

        return new Senha(hash, salt);
    }

    public static Senha Recriar(string hash, string salt) =>
        new(hash, salt);

    public bool Validar(string senhaTexto) =>
        BCrypt.Net.BCrypt.Verify(senhaTexto, Hash);

    public static implicit operator string(Senha senha) => senha.Hash;
}