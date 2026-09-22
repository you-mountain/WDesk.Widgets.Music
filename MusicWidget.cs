using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WDesk.Core;

namespace WDesk.Widgets.Music
{
    public class MusicWidget : WidgetBase
    {
        static MusicWidget()
        {
            AssemblyResolver.EnsureInitialized();
        }

        public override WidgetMetadata Metadata
        {
            get
            {
                return new WidgetMetadata
                {
                    Id = "music",
                    NameKey = "widget.music.name",
                    DescriptionKey = "widget.music.desc",
                    Category = WidgetCategory.Music,
                    Icon = "\uE8D6",
                    Author = "WDesk Team",
                    Version = "1.0.0",
                    DefaultWidth = 340,
                    DefaultHeight = 180,
                    HasSettings = true
                };
            }
        }

        public override IEnumerable<WStyle> GetStyles()
        {
            return new List<WStyle>
            {
                new WStyle { Id = "style1", Name = "Bars",    Icon = "\uE8F1", PreviewEmoji = "🎵" },
                new WStyle { Id = "style2", Name = "Wave",    Icon = "\uE7C4", PreviewEmoji = "🌊" },
                new WStyle { Id = "style3", Name = "Compact", Icon = "\uE790", PreviewEmoji = "📻" },
                new WStyle { Id = "style4", Name = "Circle",  Icon = "\uE91F", PreviewEmoji = "⭕" }
            };
        }

        public override IStyleBuilder GetStyleBuilder(string styleId)
        {
            if (styleId == "style2") return new Style.Style2();
            if (styleId == "style3") return new Style.Style3();
            if (styleId == "style4") return new Style.Style4();
            return new Style.Style1();
        }

        public override FrameworkElement CreateSettingsView(
            PlacedWidget instance,
            Action<Dictionary<string, string>> onSave)
        {
            var root = new StackPanel();

            var state = new Dictionary<string, string>();
            state["bars_count"] = GetSetting(instance, "bars_count", "32");
            state["sensitivity"] = GetSetting(instance, "sensitivity", "100");
            state["show_label"] = GetSetting(instance, "show_label", "true");
            state["show_track_info"] = GetSetting(instance, "show_track_info", "true");
            state["use_cover_color"] = GetSetting(instance, "use_cover_color", "true");
            state["show_cover"] = GetSetting(instance, "show_cover", "true");

            Action triggerLive = () =>
            {
                try { onSave(new Dictionary<string, string>(state)); }
                catch { }
            };

            // ═══ Equalizer ═══
            root.Children.Add(CreateSectionHeader("Equalizer", "Visualizer settings"));

            var barsCombo = CreateComboBox();
            AddComboItem(barsCombo, "16 bars", "16");
            AddComboItem(barsCombo, "24 bars", "24");
            AddComboItem(barsCombo, "32 bars", "32");
            AddComboItem(barsCombo, "48 bars", "48");
            AddComboItem(barsCombo, "64 bars", "64");
            SelectComboItem(barsCombo, state["bars_count"]);
            barsCombo.SelectionChanged += (s, e) =>
            {
                state["bars_count"] = GetComboTag(barsCombo, "32");
                triggerLive();
            };
            root.Children.Add(CreateRow("Bars", "Number of EQ bars", barsCombo));

            var sensCombo = CreateComboBox();
            AddComboItem(sensCombo, "50%", "50");
            AddComboItem(sensCombo, "75%", "75");
            AddComboItem(sensCombo, "100%", "100");
            AddComboItem(sensCombo, "125%", "125");
            AddComboItem(sensCombo, "150%", "150");
            AddComboItem(sensCombo, "200%", "200");
            SelectComboItem(sensCombo, state["sensitivity"]);
            sensCombo.SelectionChanged += (s, e) =>
            {
                state["sensitivity"] = GetComboTag(sensCombo, "100");
                triggerLive();
            };
            root.Children.Add(CreateRow("Sensitivity", "How responsive to audio", sensCombo));

            // ═══ Track Info ═══
            root.Children.Add(CreateSectionHeader("Track Info", "From Windows Media Session"));

            var trackInfoToggle = CreateToggle(state["show_track_info"] == "true");
            trackInfoToggle.Checked += (s, e) => { state["show_track_info"] = "true"; triggerLive(); };
            trackInfoToggle.Unchecked += (s, e) => { state["show_track_info"] = "false"; triggerLive(); };
            root.Children.Add(CreateToggleRow("Show Track Info", "Title and artist from any player", trackInfoToggle));

            var coverToggle = CreateToggle(state["show_cover"] == "true");
            coverToggle.Checked += (s, e) => { state["show_cover"] = "true"; triggerLive(); };
            coverToggle.Unchecked += (s, e) => { state["show_cover"] = "false"; triggerLive(); };
            root.Children.Add(CreateToggleRow("Show Cover", "Album art", coverToggle));

            var coverColorToggle = CreateToggle(state["use_cover_color"] == "true");
            coverColorToggle.Checked += (s, e) => { state["use_cover_color"] = "true"; triggerLive(); };
            coverColorToggle.Unchecked += (s, e) => { state["use_cover_color"] = "false"; triggerLive(); };
            root.Children.Add(CreateToggleRow("Dynamic Accent", "Extract color from album art", coverColorToggle));

            // ═══ Display ═══
            root.Children.Add(CreateSectionHeader("Display", "What to show"));

            var labelToggle = CreateToggle(state["show_label"] == "true");
            labelToggle.Checked += (s, e) => { state["show_label"] = "true"; triggerLive(); };
            labelToggle.Unchecked += (s, e) => { state["show_label"] = "false"; triggerLive(); };
            root.Children.Add(CreateToggleRow("Show Label", "Display 'NOW PLAYING'", labelToggle));

            var saveBtn = new Button
            {
                Content = "Save Settings",
                Height = 38,
                Margin = new Thickness(0, 16, 0, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            try { saveBtn.SetResourceReference(Button.StyleProperty, "BtnSmallPrimary"); } catch { }
            saveBtn.Click += (s, e) => triggerLive();
            root.Children.Add(saveBtn);

            return root;
        }

        private static string GetSetting(PlacedWidget instance, string key, string defaultVal)
        {
            if (instance.Settings.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val))
                return val;
            return defaultVal;
        }

        private static string GetComboTag(ComboBox combo, string defaultVal)
        {
            var item = combo.SelectedItem as ComboBoxItem;
            if (item != null && item.Tag is string)
                return (string)item.Tag;
            return defaultVal;
        }

        private static void AddComboItem(ComboBox combo, string content, string tag)
        {
            combo.Items.Add(new ComboBoxItem { Content = content, Tag = tag, FontSize = 11 });
        }

        private static void SelectComboItem(ComboBox combo, string tag)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var ci = combo.Items[i] as ComboBoxItem;
                if (ci != null && ci.Tag is string && (string)ci.Tag == tag)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private static ComboBox CreateComboBox()
        {
            return new ComboBox
            {
                Width = 240,
                Height = 34,
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private static CheckBox CreateToggle(bool isChecked)
        {
            return new CheckBox
            {
                IsChecked = isChecked,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0)
            };
        }

        private static StackPanel CreateSectionHeader(string title, string desc)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 12, 0, 8) };
            panel.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = desc, FontSize = 10, Opacity = 0.6, Margin = new Thickness(0, 2, 0, 0) });
            return panel;
        }

        private static StackPanel CreateRow(string label, string description, FrameworkElement content)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var left = new StackPanel { Width = 120, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            left.Children.Add(new TextBlock { Text = description, FontSize = 10, Opacity = 0.6, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
            row.Children.Add(left);

            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            right.Children.Add(content);
            row.Children.Add(right);
            return row;
        }

        private static StackPanel CreateToggleRow(string label, string description, CheckBox toggle)
        {
            return CreateRow(label, description, toggle);
        }
    }
}