using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db
{
    public class DjangoCeleryResultsChordcounter
    {
        public int Id { get; set; }

        [MaxLength(255)]
        public string GroupId { get; set; }
        public string SubTasks { get; set; }
        public int Count { get; set; }
    }
}
