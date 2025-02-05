using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LangVision {
    internal static class OverlayRenderer {
        // Constants for font sizing
        private const float MIN_FONT_SIZE = 12.0f;
        private const float MAX_FONT_SIZE = 72.0f;
        private const float DEFAULT_FONT_SIZE = 16.0f;

        /// <summary>
        /// Draws translated text on top of the frozen screen capture.
        /// </summary>
        public static Bitmap DrawTranslatedText(Bitmap baseImage, List<Processing.TranslatedText> translatedTexts) {
            Bitmap overlayImage = new Bitmap(baseImage);

            using (Graphics g = Graphics.FromImage(overlayImage)) {
                // Enable high quality rendering
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                foreach (var text in translatedTexts) {
                    if (!IsValidBoundingBox(text.BoundingBox)) continue;
                    DrawTextWithBackground(g, text.TranslatedTextValue ?? "", text.BoundingBox);
                }
            }
            return overlayImage;
        }

        /// <summary>
        /// Validates if a bounding box is usable for text rendering
        /// </summary>
        private static bool IsValidBoundingBox(Rectangle box) {
            return box.Width > 0 && box.Height > 0 &&
                   box.Width < 10000 && box.Height < 10000; // Reasonable maximum size
        }

        /// <summary>
        /// Calculates an appropriate font size based on the bounding box dimensions
        /// </summary>
        private static float CalculateFontSize(Graphics g, string text, Rectangle boundingBox) {
            // Start with a size proportional to the box height but with safe limits
            float fontSize = Math.Max(MIN_FONT_SIZE,
                Math.Min(MAX_FONT_SIZE, boundingBox.Height * 0.7f));

            // Create test font
            using (var testFont = new Font("Arial", fontSize, FontStyle.Bold, GraphicsUnit.Pixel)) {
                SizeF textSize = g.MeasureString(text, testFont);

                // If text is too wide, scale down the font size proportionally
                if (textSize.Width > boundingBox.Width) {
                    float scaleFactor = boundingBox.Width / textSize.Width;
                    fontSize *= scaleFactor;
                    fontSize = Math.Max(MIN_FONT_SIZE, Math.Min(MAX_FONT_SIZE, fontSize));
                }
            }

            return fontSize;
        }

        /// <summary>
        /// Draws text with a semi-transparent background for better readability
        /// </summary>
        private static void DrawTextWithBackground(Graphics g, string text, Rectangle boundingBox) {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Calculate appropriate font size
            float fontSize = CalculateFontSize(g, text, boundingBox);

            using (Font font = new Font("Arial", fontSize, FontStyle.Bold, GraphicsUnit.Pixel)) {
                // Measure the text with the calculated font size
                SizeF textSize = g.MeasureString(text, font);

                // Create background rectangle that fits the text
                Rectangle backgroundRect = new Rectangle(
                    boundingBox.X,
                    boundingBox.Y,
                    Math.Max(boundingBox.Width, (int)Math.Ceiling(textSize.Width)),
                    Math.Max(boundingBox.Height, (int)Math.Ceiling(textSize.Height))
                );

                // Draw semi-transparent black background
                using (SolidBrush backgroundBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0))) {
                    g.FillRectangle(backgroundBrush, backgroundRect);
                }

                // Draw text in white
                using (SolidBrush textBrush = new SolidBrush(Color.White)) {
                    // Calculate position to center text vertically in background
                    float textX = boundingBox.X;
                    float textY = boundingBox.Y + (backgroundRect.Height - textSize.Height) / 2;

                    g.DrawString(text, font, textBrush, new PointF(textX, textY));
                }

                // Draw subtle border (optional)
                using (Pen borderPen = new Pen(Color.FromArgb(100, 255, 255, 255), 1)) {
                    g.DrawRectangle(borderPen, backgroundRect);
                }
            }
        }
    }
}