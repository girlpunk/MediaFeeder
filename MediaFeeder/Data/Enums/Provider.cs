using System.ComponentModel;
namespace MediaFeeder.Data.Enums;

public enum Provider
{
    Unknown = 0,
    [Description("YouTube")]
    YouTube = 1,
    Sonarr = 3,
    RSS = 4,
    // Twitch
}
