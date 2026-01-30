using Ailos.EncryptedId;
using FluentAssertions;
using Xunit;

namespace Ailos.EncryptedId.Tests;

public class EncryptedIdTests : TestBase
{
    private readonly IEncryptedIdService _service;

    public EncryptedIdTests()
    {
        // Agora usa a propriedade herdada de TestBase
        _service = EncryptedIdFactory.CreateService(TestSecret);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(100L)]
    [InlineData(999999999L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void Encrypt_Decrypt_ShouldReturnOriginalId(long originalId)
    {
        // Act
        var encrypted = _service.Encrypt(originalId);
        var decrypted = _service.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(originalId);
        encrypted.Value.Should().NotBeNullOrEmpty();
        encrypted.Value.Should().NotContain("+").And.NotContain("/").And.NotEndWith("=");
    }

    [Fact]
    public void TryDecrypt_WithValidToken_ShouldReturnTrueAndId()
    {
        // Arrange
        var originalId = 12345L;
        var encrypted = _service.Encrypt(originalId);

        // Act
        var result = _service.TryDecrypt(encrypted.Value, out var decryptedId);

        // Assert
        result.Should().BeTrue();
        decryptedId.Should().Be(originalId);
    }

    [Fact]
    public void TryDecrypt_WithInvalidToken_ShouldReturnFalse()
    {
        // Act
        var result = _service.TryDecrypt("invalid-token", out var decryptedId);

        // Assert
        result.Should().BeFalse();
        decryptedId.Should().Be(0);
    }

    [Fact]
    public void Encrypt_WithSameId_ShouldReturnSameToken()
    {
        // Arrange
        var id = 42L;

        // Act
        var token1 = _service.Encrypt(id);
        var token2 = _service.Encrypt(id);

        // Assert
        token1.Value.Should().Be(token2.Value);
    }

    [Fact]
    public void EncryptedId_ValueObject_ShouldWorkCorrectly()
    {
        // Arrange
        var value = "test-token";

        // Act
        var encryptedId1 = new EncryptedId(value);
        var encryptedId2 = new EncryptedId(value);

        // Assert
        encryptedId1.Should().Be(encryptedId2);
        encryptedId1.GetHashCode().Should().Be(encryptedId2.GetHashCode());
        (encryptedId1 == encryptedId2).Should().BeTrue();
        encryptedId1.ToString().Should().Be(value);
        string token = encryptedId1;
        token.Should().Be(value);
    }
}
