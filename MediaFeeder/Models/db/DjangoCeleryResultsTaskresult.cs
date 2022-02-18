using System;

namespace MediaFeeder.Models.db
{
    public class DjangoCeleryResultsTaskresult
    {
        public int Id { get; set; }
        public string TaskId { get; set; }
        public string Status { get; set; }
        public string ContentType { get; set; }
        public string ContentEncoding { get; set; }
        public string Result { get; set; }
        public DateTime DateDone { get; set; }
        public string Traceback { get; set; }
        public string Meta { get; set; }
        public string TaskArgs { get; set; }
        public string TaskKwargs { get; set; }
        public string TaskName { get; set; }
        public string Worker { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
