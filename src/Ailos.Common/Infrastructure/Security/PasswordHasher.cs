using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ailos.Common.Infrastructure.Security;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hashedPassword);
    (string Hash, string Salt) HashWithSalt(string password);
    bool VerifyWithSalt(string password, string hash, string salt);
}

public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool Verify(string password, string hashedPassword)
    {
        return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }

    public (string Hash, string Salt) HashWithSalt(string password)
    {
        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        var hash = BCrypt.Net.BCrypt.HashPassword(password, salt);
        return (hash, salt);
    }

    public bool VerifyWithSalt(string password, string hash, string salt)
    {
        var computedHash = BCrypt.Net.BCrypt.HashPassword(password, salt);
        return computedHash == hash;
    }
}
