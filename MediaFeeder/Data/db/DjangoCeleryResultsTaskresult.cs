using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class DjangoCeleryResultsTaskresult
{
    public int Id { get; set; }

    [MaxLength(255)] public required string TaskId { get; set; }

    [MaxLength(50)] public required string Status { get; set; }

    [MaxLength(128)] public required string ContentType { get; set; }

    [MaxLength(64)] public required string ContentEncoding { get; set; }

    [MaxLength(1000000000)] public required string Result { get; set; }
    public DateTime DateDone { get; set; }
    [MaxLength(1000000000)] public required string Traceback { get; set; }
    [MaxLength(1000000000)] public required string Meta { get; set; }
    [MaxLength(1000000000)] public required string TaskArgs { get; set; }
    [MaxLength(1000000000)] public required string TaskKwargs { get; set; }

    [MaxLength(255)] public required string TaskName { get; set; }

    [MaxLength(100)] public required string Worker { get; set; }

    public DateTime DateCreated { get; set; }
}