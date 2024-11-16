using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db
{
    public class DjangoCeleryResultsGroupresult
    {
        public int Id { get; set; }

        [MaxLength(255)]
        public string GroupId { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateDone { get; set; }

        [MaxLength(128)]
        public string ContentType { get; set; }

        [MaxLength(64)]
        public string ContentEncoding { get; set; }
        public string Result { get; set; }
    }
}
