using BCrypt.Net; // Adicione este using

public sealed record Senha
{
    public string Hash { get; }
    private Senha(string hash)
    {
        Hash = hash;
    }

    public static Senha Criar(string senhaTexto)
    {
        if (string.IsNullOrWhiteSpace(senhaTexto) || senhaTexto.Length < 6)
            throw new ArgumentException("Senha deve ter no mínimo 6 caracteres");

        var hash = BCrypt.Net.BCrypt.HashPassword(senhaTexto);

        return new Senha(hash);
    }

    public static Senha Recriar(string hash) =>
        new(hash);

    public bool Validar(string senhaTexto) =>
        BCrypt.Net.BCrypt.Verify(senhaTexto, Hash);

    public static implicit operator string(Senha senha) => senha.Hash;
}
