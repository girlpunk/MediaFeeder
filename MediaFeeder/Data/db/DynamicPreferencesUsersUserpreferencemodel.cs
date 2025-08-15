using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class DynamicPreferencesUsersUserpreferencemodel
{
    public int Id { get; set; }

    [MaxLength(150)] public required string Section { get; set; }

    [MaxLength(150)] public required string Name { get; set; }

    [MaxLength(1000000000)] public required string RawValue { get; set; }
    public int InstanceId { get; set; }

    public virtual AuthUser? Instance { get; set; }
}
