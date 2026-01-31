// src/Ailos.ContaCorrente.Api/Domain/ValueObjects/Cpf.cs
namespace Ailos.ContaCorrente.Api.Domain.ValueObjects;

public sealed record Cpf
{
    public string Numero { get; }

    public Cpf(string numero)
    {
        if (!Validar(numero))
            throw new ArgumentException("CPF inválido");

        Numero = RemoverFormatacao(numero);
    }

    public static bool Validar(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        cpf = RemoverFormatacao(cpf);

        if (cpf.Length != 11)
            return false;

        if (cpf.All(c => c == cpf[0]))
            return false;

        // Validação dos dígitos verificadores
        int[] multiplicador1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] multiplicador2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        string tempCpf = cpf.Substring(0, 9);
        int soma = 0;

        for (int i = 0; i < 9; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador1[i];

        int resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;

        string digito = resto.ToString();
        tempCpf += digito;

        soma = 0;
        for (int i = 0; i < 10; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador2[i];

        resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;

        digito += resto.ToString();

        return cpf.EndsWith(digito);
    }

    private static string RemoverFormatacao(string cpf) =>
        new string(cpf.Where(char.IsDigit).ToArray());

    public override string ToString() => Numero;
}
