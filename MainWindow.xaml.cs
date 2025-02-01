using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace LangVision {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private GlobalHotkeyManager? hotkeyManager;

        public MainWindow() {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded; // Register hotkeys AFTER window is loaded
        }

        /// <summary>
        /// Registers hotkeys when the window is loaded
        /// </summary>
        private void MainWindow_Loaded(object _, RoutedEventArgs __) {
            hotkeyManager = new GlobalHotkeyManager(this);
            hotkeyManager.OnRegionCapture += CaptureRegion;
            hotkeyManager.OnFullscreenCapture += CaptureFullscreen;
        }

        /// <summary> Captures the whole screen </summary>
        private async void CaptureFullscreen() {
            Bitmap capturedImage = Capture.CaptureActiveScreen();
            string filePath = "screenshot_fullscreen.png";
            Capture.SaveCapture(capturedImage, filePath);

            // Process OCR & Translation
            Bitmap? translatedImage = await Processing.ProcessRegionAndReturnImage(
                new Rectangle(0, 0, capturedImage.Width, capturedImage.Height), "auto", "es");

            if (translatedImage != null)
                Capture.SaveCapture(translatedImage, "translated_fullscreen.png");
        }

        /// <summary> Captures only the selected region </summary>
        private async void CaptureRegion() {
            SelectionOverlay selectionOverlay = new SelectionOverlay();
            if (selectionOverlay.ShowDialog() == true) {
                Rectangle selectedRegion = selectionOverlay.SelectedRegion;

                if (selectedRegion.Width > 0 && selectedRegion.Height > 0) {
                    Bitmap capturedImage = Capture.CaptureScreenRegion(selectedRegion);
                    string filePath = "screenshot_region.png";
                    Capture.SaveCapture(capturedImage, filePath);

                    // Process OCR & Translation
                    Bitmap? translatedImage = await Processing.ProcessRegionAndReturnImage(selectedRegion, "auto", "es");

                    if (translatedImage != null)
                        Capture.SaveCapture(translatedImage, "translated_region.png");
                }
            }
        }

        protected override void OnClosed(EventArgs e) {
            if (hotkeyManager != null)
                hotkeyManager.UnregisterHotkeys();
            base.OnClosed(e);
        }

        /// <summary>
        /// Captures the selected region when the button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnRegionCapture_Click(object sender, RoutedEventArgs e) => CaptureRegion();

        /// <summary>
        /// Captures the whole screen when the button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnFullscreenCapture_Click(object sender, RoutedEventArgs e) => CaptureFullscreen();
    }
}