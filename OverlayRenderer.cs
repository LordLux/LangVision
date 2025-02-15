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
                    DrawTextWithOptimalFit(
                        g,
                        text.TranslatedTextValue ?? "",
                        text.BoundingBox,
                        text.TextColor,
                        text.BackgroundColor
                    );
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
                using (var font = LoadCustomFont(currentSize, FontStyle.Regular)) {
                    SizeF textSize = g.MeasureString(text, font);
                    if (textSize.Width <= bounds.Width * WIDTH_MARGIN && textSize.Height <= bounds.Height) {
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
        private static void DrawTextWithOptimalFit(Graphics g, string text, Rectangle boundingBox, Color textColor, Color bgColor) {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Calculate optimal font size
            float fontSize = CalculateOptimalFontSize(g, text, boundingBox);

            Font font = LoadCustomFont(fontSize + 4, FontStyle.Regular);
            SizeF textSize = g.MeasureString(text, font);
            float yOffset = ((boundingBox.Height - textSize.Height) / 2) + (4.5f + 3f);


            // Create background rectangle
            RectangleF backgroundRect = new RectangleF(
                boundingBox.X,
                boundingBox.Y + 4f,
                boundingBox.Width,
                boundingBox.Height
            );

            // Draw semi-transparent background
            using (SolidBrush bgBrush = new SolidBrush(bgColor)) {
                g.FillRectangle(bgBrush, backgroundRect);
            }

            // Draw text
            using (GraphicsPath path = new GraphicsPath()) {
                var textRect = new RectangleF(
                    boundingBox.X,
                    boundingBox.Y + yOffset,
                    boundingBox.Width,
                    fontSize //textSize.Height
                );

                using (StringFormat sf = new StringFormat()) {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                    sf.Trimming = StringTrimming.None;

                    path.AddString(text, font.FontFamily, (int)font.Style, font.Size, textRect, sf);
                }

                using (Pen outlinePen = new Pen(LightenColor(bgColor, 40), 1.5f) { LineJoin = LineJoin.Round }) {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawPath(outlinePen, path);
                }
                
                using (SolidBrush textBrush = new SolidBrush(textColor)) {
                    g.FillPath(textBrush, path);
                }
            }
        }

        private static Color LightenColor(Color color, int amount) {
            int r = Math.Min(255, color.R + amount);
            int g = Math.Min(255, color.G + amount);
            int b = Math.Min(255, color.B + amount);
            return Color.FromArgb(color.A, r, g, b);
        }
    }
}