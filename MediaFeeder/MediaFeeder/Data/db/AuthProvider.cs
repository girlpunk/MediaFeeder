using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class AuthProvider
{
    public int Id { get; set; }

    public int UserId { get; set; }

    [MaxLength(1000000000)]
    public required string LoginProvider { get; set; }

    [MaxLength(1000000000)]
    public required string ProviderKey { get; set; }

    public virtual AuthUser? User { get; set; }
}
