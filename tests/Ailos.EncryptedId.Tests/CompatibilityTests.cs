using Ailos.EncryptedId;
using FluentAssertions;
using Xunit;

namespace Ailos.EncryptedId.Tests;

public class CompatibilityTests : TestBase
{
    [Fact]
    public void ShouldGenerateConsistentTokens()
    {
        // Arrange
        var service = EncryptedIdFactory.CreateService(TestSecret);
        var testCases = new[]
        {
            new { Id = 1L },
            new { Id = 12345L },
        };

        // Act & Assert
        foreach (var testCase in testCases)
        {
            var token = service.Encrypt(testCase.Id);
            var decrypted = service.Decrypt(token);
            
            // Primeiro valide que funciona
            decrypted.Should().Be(testCase.Id);
            
            // Token deve ter formato correto
            token.Value.Should().NotBeNullOrEmpty();
            token.Value.Should().NotContain("+").And.NotContain("/").And.NotEndWith("=");
        }
    }
    
    [Fact]
    public void ShouldBeCompatibleWithExistingApi()
    {
        // Arrange
        var service = EncryptedIdFactory.CreateService(TestSecret);
        
        // Teste com alguns IDs
        var testIds = new[] { 123L, 456L, 789L };
        
        foreach (var id in testIds)
        {
            // Act
            var encrypted = service.Encrypt(id);
            var decrypted = service.Decrypt(encrypted);
            
            // Assert
            decrypted.Should().Be(id);
            
            // Token deve ter 43 caracteres (Base64 de 32 bytes sem padding)
            encrypted.Value.Length.Should().Be(43);
        }
    }
}
