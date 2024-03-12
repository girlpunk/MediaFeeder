using MediaFeeder.Models.db;

namespace MediaFeeder.Data
{
    public class SubscriptionFolderService
    {
        private readonly MediaFeederDataContext _dbContext;

        public SubscriptionFolderService(MediaFeederDataContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IQueryable<YtManagerAppSubscriptionFolder> GetSubscriptionFolders() => _dbContext.YtManagerAppSubscriptionFolders;

        public Dictionary<int, int> UnwatchedCounts(YtManagerAppSubscriptionFolder folder)
        {
            var subscriptionIDs = _dbContext.YtManagerAppSubscriptions
                .Select(static s => s.Id)
                .ToList();

            return _dbContext.YtManagerAppVideos
                .Where(video => subscriptionIDs.Contains(video.SubscriptionId))
                .Where(static video => !video.Watched)
                .GroupBy(static video => video.SubscriptionId)
                .ToDictionary(static g => g.Key, static g => g.Count());
        }
    }
}
