using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.API.Models.db
{
    public class DjangoCeleryResultsTaskresult
    {
        public int Id { get; set; }
        
        [MaxLength(255)]
        public string TaskId { get; set; }

        [MaxLength(50)]
        public string Status { get; set; }

        [MaxLength(128)]
        public string ContentType { get; set; }

        [MaxLength(64)]
        public string ContentEncoding { get; set; }
        public string Result { get; set; }
        public DateTime DateDone { get; set; }
        public string Traceback { get; set; }
        public string Meta { get; set; }
        public string TaskArgs { get; set; }
        public string TaskKwargs { get; set; }

        [MaxLength(255)]
        public string TaskName { get; set; }

        [MaxLength(100)]
        public string Worker { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
