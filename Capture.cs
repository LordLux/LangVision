using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace LangVision
{
    internal class Capture {
        public static int xMargin = 8;
        public static int yMargin = 4;

        /// <summary>
        /// Captures a specific region of the screen and returns it as a Bitmap.
        /// </summary>
        /// <param name="rect">The screen region to capture.</param>
        /// <returns>A Bitmap containing the captured image.</returns>
        public static Bitmap CaptureScreenRegion(Rectangle rect) {
            // Create a new bitmap with the specified dimensions
            Bitmap bitmap = new Bitmap(rect.Width, rect.Height);

            using (Graphics g = Graphics.FromImage(bitmap)) {
                // Capture the specified area of the screen
                g.CopyFromScreen(rect.Location, System.Drawing.Point.Empty, rect.Size);
            }

            return bitmap;
        }

        /// <summary>
        /// Captures the entire screen where the mouse is currently located.
        /// </summary>
        public static Bitmap CaptureActiveScreen() {
            Screen activeScreen = Screen.FromPoint(Cursor.Position);
            Rectangle screenBounds = activeScreen.Bounds;

            float c = 1.0065f;

            int correctedWidth = (int)(screenBounds.Width * c);
            int correctedHeight = (int)(screenBounds.Height * c);

            return CaptureScreenRegion(new Rectangle(screenBounds.X - xMargin, screenBounds.Y - yMargin, correctedWidth, correctedHeight));
        }

        /// <summary>
        /// Saves a captured image to a file.
        /// </summary>
        /// <param name="bitmap">The captured bitmap image.</param>
        /// <param name="filename">File path to save the image.</param>
        public static void SaveCapture(Bitmap bitmap, string filename) {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string fullPath = System.IO.Path.Combine(desktopPath, filename);

            bitmap.Save(fullPath, ImageFormat.Png);
            System.Windows.MessageBox.Show($"Screenshot saved at: {fullPath}", "Capture Successfull!", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static BitmapImage CaptureActiveScreenAsBitmapImage() {
            Bitmap bitmap = CaptureActiveScreen();
            return ConvertBitmapToImageSource(bitmap);
        }

        /// <summary>
        /// Converts a Bitmap to a BitmapImage for WPF display.
        /// </summary>
        private static BitmapImage ConvertBitmapToImageSource(Bitmap bitmap) {
            using (MemoryStream memory = new MemoryStream()) {
                bitmap.Save(memory, ImageFormat.Png);
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
