using System;

namespace MediaFeeder.Models.db
{
    public class DjangoCeleryResultsGroupresult
    {
        public int Id { get; set; }
        public string GroupId { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateDone { get; set; }
        public string ContentType { get; set; }
        public string ContentEncoding { get; set; }
        public string Result { get; set; }
    }
}
