using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db
{
    public class DjangoMigration
    {
        public int Id { get; set; }

        [MaxLength(255)]
        public string App { get; set; }

        [MaxLength(255)]
        public string Name { get; set; }
        public DateTime Applied { get; set; }
    }
}
