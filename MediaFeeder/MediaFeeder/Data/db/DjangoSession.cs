using System.ComponentModel.DataAnnotations;

namespace MediaFeeder.Data.db
{
    public class DjangoSession
    {
        [MaxLength(40)]
        public string SessionKey { get; set; }
        public string SessionData { get; set; }
        public DateTime ExpireDate { get; set; }
    }
}
