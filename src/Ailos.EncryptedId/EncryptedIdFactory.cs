namespace Ailos.EncryptedId;

public static class EncryptedIdFactory
{
    public static IEncryptedIdService CreateService(string secretKey)
    {
        return new Services.EncryptedIdService(secretKey);
    }
}
