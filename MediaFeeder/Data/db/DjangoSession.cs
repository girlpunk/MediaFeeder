using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class DjangoSession
{
    [MaxLength(40)] public required string SessionKey { get; set; }

    [MaxLength(1000000000)] public required string SessionData { get; set; }
    public DateTime ExpireDate { get; set; }
}