using System.Windows;
using System.Windows.Input;

namespace Mots;

/// <summary>Zoom de la zone de travail : la page entière est agrandie, texte et marges compris.</summary>
public partial class MainWindow
{
    private const double ZoomMin = 50;
    private const double ZoomMax = 300;
    private const double ZoomStep = 10;

    private double _zoomPercent = 100;
    private bool _updatingZoom;

    private void SetZoom(double percent)
    {
        _zoomPercent = Math.Clamp(Math.Round(percent / 5) * 5, ZoomMin, ZoomMax);
        PageScale.ScaleX = PageScale.ScaleY = _zoomPercent / 100;
        ZoomText.Content = $"{_zoomPercent:0} %";

        _updatingZoom = true;
        ZoomSlider.Value = _zoomPercent;
        _updatingZoom = false;
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Appelé aussi pendant le chargement du XAML, avant que les autres éléments existent.
        if (_updatingZoom || PageScale is null || ZoomText is null) return;
        SetZoom(e.NewValue);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoomPercent + ZoomStep);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoomPercent - ZoomStep);

    private void ZoomReset_Click(object sender, RoutedEventArgs e) => SetZoom(100);

    private void ZoomIn_Executed(object sender, ExecutedRoutedEventArgs e) => SetZoom(_zoomPercent + ZoomStep);

    private void ZoomOut_Executed(object sender, ExecutedRoutedEventArgs e) => SetZoom(_zoomPercent - ZoomStep);

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        SetZoom(_zoomPercent + Math.Sign(e.Delta) * ZoomStep);
        e.Handled = true;
    }
}
