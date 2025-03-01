﻿using System;
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
using System.IO;
using System.Drawing.Imaging;

// TODO allow the user to select and copy the translated text
// TODO replace bounding box color fill with actual text removal overlay using LaMa
// TODO if text bounding box's length < height, rotate the text 90 degrees
// TODO implement local ocr and translation
// TODO detect when no internet -> fallback to local
// TODO make program background process and not create a window

namespace LangVision {
    public partial class SelectionOverlay : Window {
        public static bool isOverlayOpen = false;
        private System.Windows.Point startPoint;
        private System.Windows.Point endPoint;
        private bool isSelecting = false;
        private bool isClosing = false;
        private bool suppressSelectionChanged = false;

        private bool _isDragging = false;
        private System.Windows.Point _mouseOffset;
        private double _originalLeft, _originalTop;

        private double topMargin = 19;
        private double bottomMargin = 40;

        private Bitmap? cachedRegion;
        private List<OCR.OCRBlock>? cachedOCRBlocks;

        public System.Drawing.Rectangle SelectedRegion { get; private set; }

        public SelectionOverlay() {
            InitializeComponent();
            isOverlayOpen = true;

            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            Storyboard fadeIn = (Storyboard)FindResource("FadeInAnimation");
            fadeIn.Begin(this);

            Cursor = System.Windows.Input.Cursors.Cross; // Crosshair cursor

            // Get a screenshot from Capture.cs and use it as background
            FrozenScreenImage.Source = Capture.CaptureActiveScreenAsBitmapImage();

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

        private void InitializeTopUI() {
            // Set the initial position of the top UI
            double canvasWidth = MainCanvas.ActualWidth;
            double borderWidth = TopUIBorder.ActualWidth;
            double left = (canvasWidth - borderWidth) / 2;
            Canvas.SetLeft(TopUIBorder, left);
            Canvas.SetTop(TopUIBorder, topMargin);

            // Initialize the language dropdown
            InitializeLanguageDropdowns();
        }

        /// <summary> Check if the overlay is not already closing and close it with fadeout </summary>
        private void CheckIfClosing() {
            if (!isClosing) {
                isClosing = true;
                suppressSelectionChanged = true;
                OutputLang.IsEnabled = false;
                isOverlayOpen = false;
                this.CloseWithFadeOut();
            }
        }

        /// <summary> Closes the overlay with a fade-out animation. </summary>
        private void CloseWithFadeOut() {
            // Start the overall fade-out animation for the overlay.
            Storyboard fadeOut = (Storyboard)this.FindResource("FadeOutAnimation");
            fadeOut.Begin(this);

            // Determine where to animate the TopUI:
            double overlayHeight = MainCanvas.ActualHeight;
            double currentTop = Canvas.GetTop(TopUIBorder);
            double targetTopForClose;

            if (currentTop > overlayHeight / 2) {
                // If TopUI is snapped at the bottom, animate it downwards (off the screen).
                targetTopForClose = overlayHeight;
            } else {
                // If it's at the top, animate it upwards (off the screen).
                targetTopForClose = -TopUIBorder.ActualHeight;
            }

            // Animate the TopUI's vertical position with an easing function.
            DoubleAnimation swipeAnimation = new DoubleAnimation {
                To = targetTopForClose,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.Stop
            };

            // When the animation completes, clear it.
            swipeAnimation.Completed += (s, e) =>
            {
                TopUIBorder.BeginAnimation(Canvas.TopProperty, null);
                Canvas.SetTop(TopUIBorder, targetTopForClose);
            };

            TopUIBorder.BeginAnimation(Canvas.TopProperty, swipeAnimation);
        }

        /// <summary> Called when fade-out animation completes to fully close the overlay. </summary>
        private void FadeOutAnimation_Completed(object sender, EventArgs e) => this.Close();

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
        private async void Window_MouseUp(object sender, MouseButtonEventArgs e) {
            if (isSelecting) {
                isSelecting = false;
                endPoint = e.GetPosition(this);

                var source = PresentationSource.FromVisual(this);
                if (source == null) {
                    CheckIfClosing();
                    return;
                }

                SelectionRectangle.Visibility = Visibility.Hidden;
                ApplyGradientOverlay();

                var selectedRegion = GetSelectedRegion(source);
                await ProcessSelection(selectedRegion, source);
            }
        }

        /// <summary> Fullscreen button click event </summary>
        private async void FullscreenButton_Click(object sender, RoutedEventArgs e) {
            Screen activeScreen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            System.Drawing.Rectangle screenBounds = activeScreen.Bounds;

            // Apply margins if necessary
            System.Drawing.Rectangle captureRegion = new System.Drawing.Rectangle(
                screenBounds.X,
                screenBounds.Y,
                screenBounds.Width,
                screenBounds.Height
            );

            // Get DPI scaling of the active screen
            var source = PresentationSource.FromVisual(this);
            if (source == null) {
                CheckIfClosing();
                return;
            }

            ApplyGradientOverlay();

            await ProcessSelection(captureRegion, source);
        }

        /// <summary> Get the selected screen region, adjusted for DPI </summary>
        private System.Drawing.Rectangle GetSelectedRegion(PresentationSource source) {
            var transformToDevice = source.CompositionTarget.TransformToDevice;

            // Transform to screen coordinates
            var startPointScreen = transformToDevice.Transform(startPoint);
            var endPointScreen = transformToDevice.Transform(endPoint);

            int x = (int)Math.Min(startPointScreen.X, endPointScreen.X) + 9;
            int y = (int)Math.Min(startPointScreen.Y, endPointScreen.Y) + 1;
            int width = (int)Math.Abs(startPointScreen.X - endPointScreen.X);
            int height = (int)Math.Abs(startPointScreen.Y - endPointScreen.Y);

            // Store the selected region with margin adjustment
            return new System.Drawing.Rectangle(
                x - Capture.xMargin - 5,
                y - Capture.yMargin - 5,
                width + 10,
                height + 10
            );
        }

        /// <summary> Process a selected screen region and display the translated text </summary>
        private async Task ProcessSelection(System.Drawing.Rectangle region, PresentationSource source) {
            if (region.Width <= 0 || region.Height <= 0) return;

            // Get DPI info
            var transformToDevice = source.CompositionTarget.TransformToDevice;
            var transformFromDevice = source.CompositionTarget.TransformFromDevice;

            // Crop the selected region from the frozen screen image
            Bitmap capturedRegion = CropFrozenScreen(region);
            if (capturedRegion == null) return;

            this.cachedRegion = capturedRegion;

            string targetLang = GetSelectedTargetLanguage();

            // Run OCR on the region and cache the result
            var ocrBlocks = await OCR.RecognizeTextBlocksFromRegion(capturedRegion);
            if (ocrBlocks.Count == 0) return;
            this.cachedOCRBlocks = ocrBlocks;

            // Process translation using the cached OCR result
            var translatedImage = await Processing.ProcessTranslationFromOCR(capturedRegion, ocrBlocks, "auto", targetLang);

            if (translatedImage != null) {
                TranslatedImage.Source = ConvertBitmapToImageSource(translatedImage);
                TranslatedImage.Visibility = Visibility.Visible;

                // Transform screen coordinates back to WPF coordinates
                var wpfPoint = transformFromDevice.Transform(new System.Windows.Point(region.X, region.Y));

                // Set position and size according to DPI scaling
                Canvas.SetLeft(TranslatedImage, wpfPoint.X);
                Canvas.SetTop(TranslatedImage, wpfPoint.Y);
                TranslatedImage.Width = region.Width / transformToDevice.M11;
                TranslatedImage.Height = (region.Height / transformToDevice.M22) + 1 + 5; // padding
            }
        }



        //// HELPER FUNCTIONS
        // <summary> Crops the selected region from the frozen screen image instead of recapturing </summary>
        private Bitmap CropFrozenScreen(System.Drawing.Rectangle region) {
            Bitmap? fullScreenshot = null;
            try {
                // Convert FrozenScreenImage (BitmapImage) to Bitmap
                fullScreenshot = ConvertBitmapImageToBitmap((BitmapImage)FrozenScreenImage.Source);

                // Create a new bitmap with the exact region
                Bitmap croppedImage = new Bitmap(region.Width, region.Height, fullScreenshot.PixelFormat);

                using (Graphics g = Graphics.FromImage(croppedImage)) {
                    g.DrawImage(fullScreenshot,
                        new System.Drawing.Rectangle(0, 0, region.Width, region.Height),
                        region,
                        GraphicsUnit.Pixel);
                }

                return croppedImage;
            } finally {
                fullScreenshot?.Dispose();
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

        /// <summary> Converts a BitmapImage (used in WPF) to a Bitmap (System.Drawing) </summary>
        private Bitmap ConvertBitmapImageToBitmap(BitmapImage bitmapImage) {
            using (MemoryStream outStream = new MemoryStream()) {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(outStream);
                return new Bitmap(outStream);
            }
        }


        //// UI Events
        /// <summary> Language dropdowns </summary>
        public class LanguageItem {
            public string Code { get; set; }
            public string DisplayName { get; set; }

            public LanguageItem(string code, string displayName) {
                Code = code;
                DisplayName = displayName;
            }

            public override string ToString() => DisplayName;
        }

        /// <summary> Initializes the language dropdowns with supported languages. </summary>
        private void InitializeLanguageDropdowns() {
            var languages = new Dictionary<string, string> {
                {"ar", "Arabic"},
                {"zh-CN", "Chinese"},
                {"cs", "Czech"},
                {"nl", "Dutch"},
                {"en", "English"},
                {"fr", "French"},
                {"de", "German"},
                {"hi", "Hindi"},
                {"id", "Indonesian"},
                {"it", "Italian"},
                {"ja", "Japanese"},
                {"ko", "Korean"},
                {"pl", "Polish"},
                {"pt", "Portuguese"},
                {"ru", "Russian"},
                {"es", "Spanish"},
                {"th", "Thai"},
                {"tr", "Turkish"},
                {"uk", "Ukrainian"},
                {"vi", "Vietnamese"}
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

        /// <summary> Get the selected target language from the dropdown </summary>
        private string GetSelectedTargetLanguage() {
            return ((LanguageItem)OutputLang.SelectedItem)?.Code ?? SettingsManager.GetSavedTargetLanguage();
        }

        /// <summary> Save the selected target language to the registry </summary>
        private async void OutputLang_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!suppressSelectionChanged && OutputLang.SelectedItem is LanguageItem selectedItem) {
                // Save the selected language to settings
                SettingsManager.SaveTargetLanguage(selectedItem.Code);

                // If a region has been processed before, re-run translation only.
                if (cachedRegion != null && cachedOCRBlocks != null) {
                    var translatedImage = await Processing.ProcessTranslationFromOCR(cachedRegion, cachedOCRBlocks, "auto", selectedItem.Code); // directly use selectedItem.Code as we've just updated it to the correct value
                    if (translatedImage != null) {
                        TranslatedImage.Source = ConvertBitmapToImageSource(translatedImage);
                    }
                }
            }
        }

        /// <summary> Close button </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            CheckIfClosing();
        }




        private void MainCanvas_Loaded(object sender, RoutedEventArgs e) {
            InitializeTopUI();
        }

        // Drag start: record the offset and capture the mouse.
        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            _isDragging = true;
            // Get mouse offset within TopUIBorder
            _mouseOffset = e.GetPosition(TopUIBorder);

            // Capture the mouse on the drag handle
            DragHandle.CaptureMouse();

            // Record current positions
            _originalLeft = Canvas.GetLeft(TopUIBorder);
            if (double.IsNaN(_originalLeft)) {
                _originalLeft = (MainCanvas.ActualWidth - TopUIBorder.ActualWidth) / 2;
                Canvas.SetLeft(TopUIBorder, _originalLeft);
            }

            _originalTop = Canvas.GetTop(TopUIBorder);
            if (double.IsNaN(_originalTop)) {
                _originalTop = topMargin;
                Canvas.SetTop(TopUIBorder, _originalTop);
            }
            e.Handled = true;
        }

        // Dragging: update position in both horizontal and vertical directions.
        private void DragHandle_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) {
            if (_isDragging) {
                // Get current mouse position relative to the Canvas.
                System.Windows.Point currentPos = e.GetPosition(MainCanvas);
                double newLeft = currentPos.X - _mouseOffset.X;
                double newTop = currentPos.Y - _mouseOffset.Y - 19;

                Canvas.SetLeft(TopUIBorder, newLeft);
                Canvas.SetTop(TopUIBorder, newTop);
                e.Handled = true;
            }
        }

        // Drag end: snap horizontally to center and vertically to top or bottom.
        private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (_isDragging) {
                _isDragging = false;
                DragHandle.ReleaseMouseCapture();
                e.Handled = true;

                double currentTop = Canvas.GetTop(TopUIBorder);
                double currentLeft = Canvas.GetLeft(TopUIBorder);
                double overlayHeight = MainCanvas.ActualHeight;
                double overlayWidth = MainCanvas.ActualWidth;
                double borderHeight = TopUIBorder.ActualHeight;

                // Compute vertical snapping:
                double distanceToTop = currentTop;
                double distanceToBottom = overlayHeight - (currentTop + borderHeight);
                double targetTop = (distanceToTop < distanceToBottom)
                    ? topMargin
                    : overlayHeight - borderHeight - bottomMargin;

                // Horizontal snapping: always center.
                double targetLeft = (overlayWidth - TopUIBorder.ActualWidth) / 2;

                // Create animations with FillBehavior=Stop.
                DoubleAnimation animLeft = new DoubleAnimation {
                    To = targetLeft,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                };

                DoubleAnimation animTop = new DoubleAnimation {
                    To = targetTop,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                };

                // When the vertical animation completes, clear the animations and set final positions.
                animTop.Completed += (s, ev) =>
                {
                    TopUIBorder.BeginAnimation(Canvas.LeftProperty, null);
                    TopUIBorder.BeginAnimation(Canvas.TopProperty, null);
                    Canvas.SetLeft(TopUIBorder, targetLeft);
                    Canvas.SetTop(TopUIBorder, targetTop);
                };

                TopUIBorder.BeginAnimation(Canvas.LeftProperty, animLeft);
                TopUIBorder.BeginAnimation(Canvas.TopProperty, animTop);
            }
        }
    }
}