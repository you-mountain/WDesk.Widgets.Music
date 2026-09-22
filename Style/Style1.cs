using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WDesk.Core;

namespace WDesk.Widgets.Music.Style;

public class Style1 : IStyleBuilder
{
    public string StyleId => "style1";

    public FrameworkElement Build(PlacedWidget instance)
    {
        var root = new Grid();

        var bg = new Border
        {
            CornerRadius = new CornerRadius(20)
        };
        bg.SetResourceReference(Border.BackgroundProperty, "WidgetBg");
        root.Children.Add(bg);

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16)
        };

        var title = new TextBlock
        {
            Text = "Music".ToUpper(),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "WidgetTextMuted");
        stack.Children.Add(title);

        var value = new TextBlock
        {
            Text = "Art",
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        value.SetResourceReference(TextBlock.ForegroundProperty, "WidgetTextPrimary");
        stack.Children.Add(value);

        var subtitle = new TextBlock
        {
            Text = "Ready",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "WidgetTextSecondary");
        stack.Children.Add(subtitle);

        root.Children.Add(stack);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            subtitle.Text = DateTime.Now.ToString("HH:mm:ss");
        };
        timer.Start();

        root.Unloaded += (_, _) => timer.Stop();

        return root;
    }
}
