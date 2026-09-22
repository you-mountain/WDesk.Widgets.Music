using System.Windows;
using WDesk.Core;
using WDesk.Widgets.Music.Style;

namespace WDesk.Widgets.Music;

public class MusicWidget : WidgetBase
{
    public override WidgetMetadata Metadata { get; } = new()
    {
        Id = "music",
        NameKey = "widget.music.name",
        DescriptionKey = "widget.music.desc",
        Category = WidgetCategory.Music,
        Icon = "\uE790",
        Author = "WDesk Team",
        Version = "1.0.0",
        DefaultWidth = 240,
        DefaultHeight = 180,
        HasSettings = false
    };

    public override FrameworkElement CreateView(PlacedWidget instance)
    {
        return new Style1().Build(instance);
    }
}
