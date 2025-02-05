using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Transactions;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace LangVision {
    public partial class SelectionOverlay : Window {
        private System.Windows.Point startPoint;
        private System.Windows.Point endPoint;
        private bool isSelecting = false;
        private bool isClosing = false;

        public System.Drawing.Rectangle SelectedRegion { get; private set; }

        public SelectionOverlay() {
            InitializeComponent();

            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            
            //System.Windows.Media.CompositionTarget.Rendering -= OnRendering;
            //System.Windows.Media.CompositionTarget.Rendering += OnRendering;

            Storyboard fadeIn = (Storyboard)FindResource("FadeInAnimation");
            fadeIn.Begin(this);

            Cursor = System.Windows.Input.Cursors.Cross; // Crosshair cursor

            // Get a screenshot from Capture.cs and use it as background
            FrozenScreenImage.Source = Capture.CaptureActiveScreenAsBitmapImage();

            // Detect when the overlay loses focus (Alt+Tab, Win+Tab)
            this.Deactivated += (s, e) => {
                this.CheckIfClosing();
            };
        }

        /// <summary> Check if the overlay is not already closing and close it with fadeout </summary>
        private void CheckIfClosing() {
            if (!isClosing) {
                isClosing = true;
                this.CloseWithFadeOut();
            }
        }

        /// <summary> Closes the overlay with a fade-out animation. </summary>
        private void CloseWithFadeOut() {
            Storyboard fadeOut = (Storyboard)FindResource("FadeOutAnimation");
            fadeOut.Begin(this);
        }

        /// <summary> Called when fade-out animation completes to fully close the overlay. </summary>
        private void FadeOutAnimation_Completed(object sender, EventArgs e) => this.Close(); // Now it closes after fade-out

        /// <summary> Apply gradient overlay after region selection </summary>
        private void ApplyGradientOverlay() {
            LinearGradientBrush gradient = new LinearGradientBrush {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1)
            };
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(180, 0, 0, 0), 0.0)); // Darker at top-left
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(120, 100, 100, 100), 0.5)); // Lighter in center
            gradient.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(80, 200, 200, 200), 1.0)); // Even lighter at bottom-right

            OverlayBackground.Fill = gradient;
        }

        /// <summary> Esc -> Close overlay </summary>
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e) {
            if (e.Key == Key.Escape) CheckIfClosing();
        }


        /// Mouse events for region selection
        // Start region selection
        private void Window_MouseDown(object _, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                isSelecting = true;
                startPoint = e.GetPosition(this);

                SelectionRectangle.Width = 0;
                SelectionRectangle.Height = 0;
                SelectionRectangle.Visibility = Visibility.Visible;

                Canvas.SetLeft(SelectionRectangle, startPoint.X);
                Canvas.SetTop(SelectionRectangle, startPoint.Y);
            }
        }

        // Update region
        private void Window_MouseMove(object _, System.Windows.Input.MouseEventArgs e) {
            if (isSelecting) {
                endPoint = e.GetPosition(this);

                double x = Math.Min(startPoint.X, endPoint.X);
                double y = Math.Min(startPoint.Y, endPoint.Y);
                double width = Math.Abs(startPoint.X - endPoint.X);
                double height = Math.Abs(startPoint.Y - endPoint.Y);

                Canvas.SetLeft(SelectionRectangle, x);
                Canvas.SetTop(SelectionRectangle, y);
                SelectionRectangle.Width = width;
                SelectionRectangle.Height = height;
            }
        }

        // Finished region selection
        // In SelectionOverlay.xaml.cs, update the Window_MouseUp method:
        // In SelectionOverlay.xaml.cs:
        private async void Window_MouseUp(object sender, MouseButtonEventArgs e) {
            if (isSelecting) {
                isSelecting = false;
                endPoint = e.GetPosition(this);

                var source = PresentationSource.FromVisual(this);
                if (source == null) {
                    CheckIfClosing();
                    return;
                }

                // Get DPI info
                var transformToDevice = source.CompositionTarget.TransformToDevice;
                var transformFromDevice = source.CompositionTarget.TransformFromDevice;

                // Transform to screen coordinates
                var startPointScreen = transformToDevice.Transform(startPoint);
                var endPointScreen = transformToDevice.Transform(endPoint);

                // Calculate region dimensions
                int x = (int)Math.Min(startPointScreen.X, endPointScreen.X);
                int y = (int)Math.Min(startPointScreen.Y, endPointScreen.Y);
                int width = (int)Math.Abs(startPointScreen.X - endPointScreen.X);
                int height = (int)Math.Abs(startPointScreen.Y - endPointScreen.Y);

                // Store the selected region with margin adjustment
                SelectedRegion = new System.Drawing.Rectangle(
                    x - Capture.xMargin,
                    y - Capture.yMargin,
                    width,
                    height
                );

                SelectionRectangle.Visibility = Visibility.Hidden;

                // Process the region and get only the translated overlay
                var translatedImage = await Processing.ProcessRegionAndReturnImage(SelectedRegion, "auto", "es");

                if (translatedImage != null) {
                    TranslatedImage.Source = ConvertBitmapToImageSource(translatedImage);
                    TranslatedImage.Visibility = Visibility.Visible;

                    // Transform back to WPF coordinates for positioning
                    var wpfPoint = transformFromDevice.Transform(new System.Windows.Point(x, y));

                    // Position the translation overlay
                    Canvas.SetLeft(TranslatedImage, wpfPoint.X);
                    Canvas.SetTop(TranslatedImage, wpfPoint.Y);

                    // Set size with DPI adjustment
                    TranslatedImage.Width = width / transformToDevice.M11;
                    TranslatedImage.Height = height / transformToDevice.M22;
                }
            }
        }


        /// <summary> Converts a Bitmap to a BitmapImage (WPF format) </summary>
        private BitmapImage ConvertBitmapToImageSource(Bitmap bitmap) {
            using (var memory = new System.IO.MemoryStream()) {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }
    }
}
