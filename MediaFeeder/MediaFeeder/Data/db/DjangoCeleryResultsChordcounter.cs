using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class DjangoCeleryResultsChordcounter
{
    public int Id { get; set; }

    [MaxLength(255)] public required string GroupId { get; set; }

    [MaxLength(1000000000)] public required string SubTasks { get; set; }
    public int Count { get; set; }
}