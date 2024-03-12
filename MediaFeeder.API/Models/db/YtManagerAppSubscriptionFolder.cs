using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.API.Models.db
{
    public class YtManagerAppSubscriptionFolder
    {
        public int Id { get; set; }
        [MaxLength(250)]
        public required string Name { get; set; }
        public int? ParentId { get; set; }
        public int UserId { get; set; }

        public virtual YtManagerAppSubscriptionFolder? Parent { get; set; }
        public virtual required AuthUser User { get; set; }
        public virtual required ICollection<YtManagerAppSubscriptionFolder> InverseParent { get; init; }
        public virtual required ICollection<YtManagerAppSubscription> YtManagerAppSubscriptions { get; init; }
    }
}
