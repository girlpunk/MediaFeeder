using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db
{
    public class YtManagerAppJobmessage
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public double? Progress { get; set; }
        [MaxLength(1024)]
        public string Message { get; set; }
        public int Level { get; set; }
        public bool SuppressNotification { get; set; }
        public int JobId { get; set; }

        public virtual YtManagerAppJobexecution Job { get; set; }
    }
}
