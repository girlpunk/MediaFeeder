using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db
{
    public class DynamicPreferencesGlobalpreferencemodel
    {
        public int Id { get; set; }
        
        [MaxLength(150)]
        public string Section { get; set; }

        [MaxLength(150)]
        public string Name { get; set; }
        public string RawValue { get; set; }
    }
}
