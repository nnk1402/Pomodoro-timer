using System.Text.Json.Serialization;

namespace Pomodoro.Features.Background;

[JsonConverter(typeof(JsonStringEnumConverter<BackgroundKind>))]
public enum BackgroundKind { Default, Image, Video }

public sealed record BackgroundOptions(BackgroundKind Kind = BackgroundKind.Default, string? Path = null,
    double Darkness = 0.25, double SurfaceOpacity = 0.5)
{
    [JsonIgnore]
    public bool IsValid => Enum.IsDefined(Kind) && double.IsFinite(Darkness) && Darkness is >= 0 and <= 0.85
        && double.IsFinite(SurfaceOpacity) && SurfaceOpacity is >= 0 and <= 0.80;
}
