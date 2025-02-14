using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace LangVision {
    internal static class OverlayRenderer {
        private const float MIN_FONT_SIZE = 8.0f;
        private const float MAX_FONT_SIZE = 72.0f;
        private const float INITIAL_SIZE_RATIO = 0.9f;
        private const float WIDTH_MARGIN = 0.98f; // Allow text to use 98% of the box width

        /// <summary>
        /// Draws translated text on top of the frozen screen capture.
        /// </summary>
        public static Bitmap DrawTranslatedText(Bitmap baseImage, List<Processing.TranslatedText> translatedTexts) {
            Bitmap overlayImage = new Bitmap(baseImage);

            using (Graphics g = Graphics.FromImage(overlayImage)) {
                g.Clear(Color.Transparent);

                // Enable high quality text rendering
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                foreach (var text in translatedTexts) {
                    if (!IsValidBoundingBox(text.BoundingBox)) continue;
                    var newBoundingBox = text.BoundingBox;
                    newBoundingBox.Height += 2;
                    newBoundingBox.Y -= 1;
                    newBoundingBox.Width += 2;
                    newBoundingBox.X -= 1;
                    DrawTextWithOptimalFit(g, text.TranslatedTextValue ?? "", text.BoundingBox);
                }
            }
            return overlayImage;
        }

        private static bool IsValidBoundingBox(Rectangle box) {
            return box.Width > 0 && box.Height > 0 &&
                   box.Width < 10000 && box.Height < 10000;
        }

        /// <summary>
        /// Calculates the optimal font size to fit text within the given bounds
        /// </summary>
        private static float CalculateOptimalFontSize(Graphics g, string text, Rectangle bounds) {
            float fontSize = bounds.Height * INITIAL_SIZE_RATIO;
            fontSize = Math.Min(MAX_FONT_SIZE, fontSize);

            // Binary search for the optimal font size
            float minSize = MIN_FONT_SIZE;
            float maxSize = fontSize;
            float optimalSize = fontSize;

            while (maxSize - minSize > 0.5f) {
                float currentSize = (minSize + maxSize) / 2;
                using (var font = new Font("Arial", currentSize, FontStyle.Bold, GraphicsUnit.Pixel)) {
                    SizeF textSize = g.MeasureString(text, font);
                    if (textSize.Width <= bounds.Width * WIDTH_MARGIN &&
                        textSize.Height <= bounds.Height) {
                        minSize = currentSize;
                        optimalSize = currentSize;
                    } else {
                        maxSize = currentSize;
                    }
                }
            }

            return optimalSize;
        }
        private static Font LoadCustomFont(float fontSize, FontStyle fontStyle) {
            string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Fonts", "ProductSans.ttf");

            PrivateFontCollection pfc = new PrivateFontCollection();
            pfc.AddFontFile(fontPath);

            return new Font(pfc.Families[0], fontSize, fontStyle, GraphicsUnit.Pixel);
        }

        /// <summary>
        /// Draws text with optimal size and positioning within the bounding box
        /// </summary>
        private static void DrawTextWithOptimalFit(Graphics g, string text, Rectangle boundingBox) {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Calculate optimal font size
            float fontSize = CalculateOptimalFontSize(g, text, boundingBox);

            Font font = LoadCustomFont(fontSize + 4, FontStyle.Regular);
            SizeF textSize = g.MeasureString(text, font);
            float yOffset = ((boundingBox.Height - textSize.Height) / 2) + 4.5f;

            // Create background rectangle
            Rectangle backgroundRect = new Rectangle(
                boundingBox.X + 1,
                boundingBox.Y + 5,
                boundingBox.Width,
                boundingBox.Height
            );

            // Draw semi-transparent background
            //using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0))) {
            //    g.FillRectangle(bgBrush, backgroundRect);
            //}

            // Draw text
            using (var textBrush = new SolidBrush(Color.White)) {
                var textRect = new RectangleF(
                    boundingBox.X,
                    boundingBox.Y + yOffset,
                    boundingBox.Width,
                    textSize.Height
                );

                // Create string format for left alignment
                using (var sf = new StringFormat()) {
                    sf.Alignment = StringAlignment.Near; // Left alignment
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap |
                                   StringFormatFlags.NoClip;
                    sf.Trimming = StringTrimming.None;

                    g.DrawString(text, font, textBrush, textRect, sf);
                }
            }

            // Optionally, draw a subtle border for debugging
            //using (var borderPen = new Pen(Color.FromArgb(50, 255, 255, 255))) {
            //    g.DrawRectangle(borderPen, backgroundRect);
            //}
        }
    }
}