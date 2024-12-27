namespace MediaFeeder.Data.db;

public class AuthProvider
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string LoginProvider { get; set; }

    public string ProviderKey { get; set; }

    public virtual AuthUser User { get; set; }
}