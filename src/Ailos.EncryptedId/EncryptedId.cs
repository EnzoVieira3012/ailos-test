namespace Ailos.EncryptedId;

public readonly struct EncryptedId
{
    public string Value { get; }

    public EncryptedId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("EncryptedId inválido.");

        Value = value;
    }

    public override string ToString() => Value;
    public override int GetHashCode() => Value.GetHashCode();
    
    public override bool Equals(object? obj) =>
        obj is EncryptedId other && Value == other.Value;

    public static implicit operator string(EncryptedId id) => id.Value;
    public static implicit operator EncryptedId(string value) => new(value);
    
    public static bool operator ==(EncryptedId left, EncryptedId right) => left.Equals(right);
    public static bool operator !=(EncryptedId left, EncryptedId right) => !left.Equals(right);
}
