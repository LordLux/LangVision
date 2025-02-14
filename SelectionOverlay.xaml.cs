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
        private bool suppressSelectionChanged = false;

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

            // Initialize language dropdowns
            InitializeLanguageDropdowns();

            // Detect when the overlay loses focus (Alt+Tab, Win+Tab)
            this.Deactivated += (s, e) => {
                this.CheckIfClosing();
            };

            // Force focus and activation
            this.Loaded += (s, e) => {
                this.Activate();
                this.Focus();
            };
        }

        /// <summary> Check if the overlay is not already closing and close it with fadeout </summary>
        private void CheckIfClosing() {
            if (!isClosing) {
                isClosing = true;
                suppressSelectionChanged = true;
                OutputLang.IsEnabled = false;
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

                // Get selected target language
                string targetLang = GetSelectedTargetLanguage();

                // Process with selected language
                var translatedImage = await Processing.ProcessRegionAndReturnImage(SelectedRegion, "auto", targetLang); // always auto

                if (translatedImage != null) {
                    TranslatedImage.Source = ConvertBitmapToImageSource(translatedImage);
                    TranslatedImage.Visibility = Visibility.Visible;

                    // Transform back to WPF coordinates for positioning
                    var wpfPoint = transformFromDevice.Transform(new System.Windows.Point(x, y));

                    // Position the translation overlay
                    Canvas.SetLeft(TranslatedImage, wpfPoint.X);
                    Canvas.SetTop(TranslatedImage, wpfPoint.Y + Capture.yMargin);

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


        public class LanguageItem {
            public string Code { get; set; }
            public string DisplayName { get; set; }

            public LanguageItem(string code, string displayName) {
                Code = code;
                DisplayName = displayName;
            }

            public override string ToString() => DisplayName;
        }

        private void InitializeLanguageDropdowns() {
            var languages = new Dictionary<string, string> {
            {"en", "English"},
            {"es", "Spanish"},
            {"fr", "French"},
            {"de", "German"},
            {"it", "Italian"},
            {"pt", "Portuguese"},
            {"ru", "Russian"},
            {"ja", "Japanese"},
            {"ko", "Korean"},
            {"zh-CN", "Chinese (Simplified)"}
            // Add more languages as needed
        };

            // Source language (auto-detect)
            //InputLang.Items.Add(new LanguageItem("auto", "Auto-detect"));
            //InputLang.SelectedIndex = 0;
            //InputLang.IsEnabled = false; // Lock to auto-detect for now

            // Target language
            foreach (var lang in languages) {
                OutputLang.Items.Add(new LanguageItem(lang.Key, lang.Value));
            }

            // Load saved language from registry
            string savedLanguage = SettingsManager.GetSavedTargetLanguage();
            OutputLang.SelectedIndex = Array.FindIndex(
                OutputLang.Items.Cast<LanguageItem>().ToArray(),
                item => item.Code == savedLanguage
            );
        }

        private string GetSelectedTargetLanguage() {
            return ((LanguageItem)OutputLang.SelectedItem)?.Code ?? SettingsManager.GetSavedTargetLanguage();
        }
        private void OutputLang_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!suppressSelectionChanged && OutputLang.SelectedItem is LanguageItem selectedItem) {
                SettingsManager.SaveTargetLanguage(selectedItem.Code);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            CheckIfClosing();
        }
    }
}
