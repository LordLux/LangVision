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
        }

        /// <summary> Captures only the selected region </summary>
        private void CaptureRegion() {
            SelectionOverlay selectionOverlay = new SelectionOverlay();
            selectionOverlay.ShowDialog();
        }

        protected override void OnClosed(EventArgs e) {
            hotkeyManager?.UnregisterHotkeys();
            base.OnClosed(e);
        }
    }
}