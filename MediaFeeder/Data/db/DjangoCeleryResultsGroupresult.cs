using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class DjangoCeleryResultsGroupresult
{
    public int Id { get; set; }

    [MaxLength(255)] public required string GroupId { get; set; }

    public DateTime DateCreated { get; set; }
    public DateTime DateDone { get; set; }

    [MaxLength(128)] public required string ContentType { get; set; }

    [MaxLength(64)] public required string ContentEncoding { get; set; }

    [MaxLength(1000000000)] public required string Result { get; set; }
}
