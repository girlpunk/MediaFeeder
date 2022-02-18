using System.Collections.Generic;
using System.Linq;

namespace MediaFeeder.Models.db
{
    public class YtManagerAppSubscriptionFolder
    {
        public YtManagerAppSubscriptionFolder()
        {
            InverseParent = new HashSet<YtManagerAppSubscriptionFolder>();
            YtManagerAppSubscriptions = new HashSet<YtManagerAppSubscription>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public int? ParentId { get; set; }
        public int UserId { get; set; }

        public virtual YtManagerAppSubscriptionFolder? Parent { get; set; }
        public virtual AuthUser User { get; set; }
        public virtual ICollection<YtManagerAppSubscriptionFolder> InverseParent { get; set; }
        public virtual ICollection<YtManagerAppSubscription> YtManagerAppSubscriptions { get; set; }
    }
}
