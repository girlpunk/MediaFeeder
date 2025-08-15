using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class DjangoMigration
{
    public int Id { get; set; }

    [MaxLength(255)] public required string App { get; set; }

    [MaxLength(255)] public required string Name { get; set; }

    public DateTime Applied { get; set; }
}