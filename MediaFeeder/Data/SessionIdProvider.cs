namespace MediaFeeder.Data;

public class SessionIdProvider
{
    public Guid Guid { get; set; } = Guid.NewGuid();
}
