using MediaFeeder.DTOs.Enums;

namespace MediaFeeder.Web;

public interface IProvider
{
    public Type VideoFrameView { get; }
    public Provider Provider { get; }
}
