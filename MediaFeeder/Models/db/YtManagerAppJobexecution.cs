using System;
using System.Collections.Generic;

namespace MediaFeeder.Models.db
{
    public class YtManagerAppJobexecution
    {
        public YtManagerAppJobexecution()
        {
            YtManagerAppJobmessages = new HashSet<YtManagerAppJobmessage>();
        }

        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Description { get; set; }
        public int Status { get; set; }
        public int? UserId { get; set; }

        public virtual AuthUser User { get; set; }
        public virtual ICollection<YtManagerAppJobmessage> YtManagerAppJobmessages { get; set; }
    }
}
