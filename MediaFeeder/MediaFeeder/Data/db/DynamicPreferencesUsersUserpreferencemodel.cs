using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db;

public class DynamicPreferencesUsersUserpreferencemodel
{
    public int Id { get; set; }

    [MaxLength(150)] public string Section { get; set; }

    [MaxLength(150)] public string Name { get; set; }

    public string RawValue { get; set; }
    public int InstanceId { get; set; }

    public virtual AuthUser Instance { get; set; }
}