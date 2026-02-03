using System.Security.Cryptography;
using System.Text;

namespace Ailos.EncryptedId.Services;

public sealed class EncryptedIdService : IEncryptedIdService
{
    private readonly byte[] _key;

    public EncryptedIdService(string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("A chave secreta não pode ser vazia", nameof(secretKey));
        
        using var sha = SHA256.Create();
        _key = sha.ComputeHash(Encoding.UTF8.GetBytes(secretKey));
    }

    public EncryptedId Encrypt(long id)
    {
        var payload = BuildPayload(id);
        var cipher = AesEncrypt(payload);
        var token = Base64UrlEncode(cipher);

        return new EncryptedId(token);
    }

    public long Decrypt(EncryptedId encryptedId)
    {
        var cipher = Base64UrlDecode(encryptedId.Value);
        var payload = AesDecrypt(cipher);
        
        ValidatePayload(payload, out var id);
        
        return id;
    }

    public bool TryDecrypt(string encryptedValue, out long id)
    {
        id = 0;
        
        try
        {
            var cipher = Base64UrlDecode(encryptedValue);
            var payload = AesDecrypt(cipher);
            
            if (!ValidatePayload(payload, out var decryptedId))
                return false;
                
            id = decryptedId;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private byte[] BuildPayload(long id)
    {
        var buffer = new byte[32];

        BitConverter.GetBytes(id).CopyTo(buffer, 0);

        using var hmac = new HMACSHA256(_key);
        var nonce = hmac.ComputeHash(BitConverter.GetBytes(id));
        Array.Copy(nonce, 0, buffer, 8, 8);

        var signature = hmac.ComputeHash(buffer[..16]);
        Array.Copy(signature, 0, buffer, 16, 16);

        return buffer;
    }

    private bool ValidatePayload(byte[] payload, out long id)
    {
        id = BitConverter.ToInt64(payload, 0);
        
        using var hmac = new HMACSHA256(_key);
        
        var expectedSignature = hmac.ComputeHash(payload[..16]);
        
        for (int i = 0; i < 16; i++)
        {
            if (payload[16 + i] != expectedSignature[i])
                return false;
        }
        
        return true;
    }

    private byte[] AesEncrypt(byte[] input)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(input, 0, input.Length);
    }

    private byte[] AesDecrypt(byte[] input)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(input, 0, input.Length);
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

    private static byte[] Base64UrlDecode(string input)
    {
        input = input.Replace("-", "+").Replace("_", "/");
        while (input.Length % 4 != 0)
            input += "=";

        return Convert.FromBase64String(input);
    }
}
