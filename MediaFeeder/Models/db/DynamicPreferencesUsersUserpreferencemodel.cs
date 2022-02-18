namespace MediaFeeder.Models.db
{
    public class DynamicPreferencesUsersUserpreferencemodel
    {
        public int Id { get; set; }
        public string Section { get; set; }
        public string Name { get; set; }
        public string RawValue { get; set; }
        public int InstanceId { get; set; }

        public virtual AuthUser Instance { get; set; }
    }
}
