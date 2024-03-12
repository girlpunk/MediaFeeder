using System;

namespace MediaFeeder.Models.db
{
    public class DjangoMigration
    {
        public int Id { get; set; }
        public string App { get; set; }
        public string Name { get; set; }
        public DateTime Applied { get; set; }
    }
}
