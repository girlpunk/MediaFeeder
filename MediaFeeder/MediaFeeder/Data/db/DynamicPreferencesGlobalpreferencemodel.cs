using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class DynamicPreferencesGlobalpreferencemodel
{
    public int Id { get; set; }

    [MaxLength(150)] public required string Section { get; set; }

    [MaxLength(150)] public required string Name { get; set; }

    [MaxLength(1000000000)] public required string RawValue { get; set; }
}