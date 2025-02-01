using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LangVision {
    internal static class OverlayRenderer {
        /// <summary>
        /// Draws translated text on top of the frozen screen capture.
        /// </summary>
        public static Bitmap DrawTranslatedText(Bitmap baseImage, List<Processing.TranslatedText> translatedTexts) {
            Bitmap overlayImage = new Bitmap(baseImage);
            using (Graphics g = Graphics.FromImage(overlayImage)) {
                foreach (var text in translatedTexts) {
                    DrawTextWithOutline(g, text.TranslatedTextValue ?? "", text.BoundingBox, Color.Yellow, Color.Transparent);
                }
            }
            return overlayImage;
        }

        /// <summary>
        /// Draws text with an outline for better visibility.
        /// </summary>
        private static void DrawTextWithOutline(Graphics g, string text, Rectangle boundingBox, Color fillColor, Color outlineColor) {
            using (Font font = new Font("Arial", boundingBox.Height, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush fillBrush = new SolidBrush(fillColor))
            using (Pen outlinePen = new Pen(outlineColor, 3)) {
                Point textPosition = new Point(boundingBox.X, boundingBox.Y);

                // Draw outline
                g.DrawRectangle(outlinePen, boundingBox);

                // Draw text
                g.DrawString(text, font, fillBrush, textPosition);
            }
        }
    }
}
