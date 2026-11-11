using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PincabVolume.Views;

/// <summary>Button that captures a key / key combo / gamepad combo into <see cref="Value"/>.</summary>
public partial class BindingPicker : UserControl
{
    const string KeyboardGlyph = "\uE765", PadGlyph = "\uE7FC", RecordGlyph = "\uE7C8", EmptyGlyph = "\uE712";

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(BindingPicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((BindingPicker)d).Refresh()));

    bool _capturing;

    public BindingPicker()
    {
        InitializeComponent();
        Refresh();
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    void Refresh()
    {
        bool isPad = Value?.StartsWith("Pad:") == true;
        bool empty = string.IsNullOrEmpty(Value);

        Glyph.Text = _capturing ? RecordGlyph : empty ? EmptyGlyph : isPad ? PadGlyph : KeyboardGlyph;
        Caption.Text = _capturing ? "Press a key or pad buttons…  (Esc cancels)"
            : empty ? "Not set"
            : (isPad ? Value[4..] : Value).Replace("+", " + ");

        var dim = (Brush)FindResource("TextDimBrush");
        var accent = (Brush)FindResource("AccentBrush");
        Glyph.Foreground = _capturing ? accent : dim;
        Caption.Foreground = _capturing ? accent : empty ? dim : (Brush)FindResource("TextBrush");
        if (_capturing) Pick.BorderBrush = accent;
        else Pick.ClearValue(BorderBrushProperty);
        Clear.IsEnabled = !empty;
    }

    void Pick_Click(object sender, RoutedEventArgs e)
    {
        _capturing = true;
        Refresh();
        App.Input.BeginCapture(result =>
        {
            _capturing = false;
            if (result != null) Value = result;
            Refresh();
        });
    }

    void Clear_Click(object sender, RoutedEventArgs e) => Value = null;
}
