namespace Ailos.EncryptedId;

public interface IEncryptedIdService
{
    EncryptedId Encrypt(long id);
    long Decrypt(EncryptedId encryptedId);
    bool TryDecrypt(string encryptedValue, out long id);
}
