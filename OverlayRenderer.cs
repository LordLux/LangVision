using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Media.TextFormatting;

namespace LangVision {
    internal static class OverlayRenderer {
        private const float MIN_FONT_SIZE = 8.0f;
        private const float MAX_FONT_SIZE = 72.0f;
        private const float INITIAL_SIZE_RATIO = 0.9f;
        private const float WIDTH_MARGIN = 0.999f; // Allow text to use 98% of the box width


        private static bool IsValidBoundingBox(Rectangle box) {
            return box.Width > 0 && box.Height > 0 &&
                   box.Width < 10000 && box.Height < 10000;
        }

        /// <summary>
        /// Calculates the optimal font size to fit text within the given bounds
        /// </summary>
        private static float CalculateOptimalFontSize(Graphics g, string text, Rectangle bounds) {
            // Define a minimum and maximum font size.
            float minSize = MIN_FONT_SIZE;
            float maxSize = bounds.Height;

            // The maximum size is the available height.
            float optimalSize = minSize;

            // Binary search loop: iterate until the difference between max and min is small.
            while (maxSize - minSize > 0.5f) {
                float testSize = (minSize + maxSize) / 2;
                using (var testFont = LoadCustomFont(testSize, FontStyle.Regular)) {
                    // Measure the text dimensions with the test font.
                    SizeF textSize = g.MeasureString(text, testFont);
                    // Check if the text fits within the bounding box.
                    if (textSize.Width <= bounds.Width) {
                        // If no overflow horizontally, check height usage
                        float neededHeight = bounds.Height;
                        if (textSize.Height < neededHeight) {
                            optimalSize = testSize;
                            // Try increasing the size for maximum utilization
                            minSize = testSize + 0.1f;
                        } else {
                            // If height is exceeded, reduce the size
                            maxSize = testSize - 0.1f;
                        }
                    } else {
                        // If overflow horizontally, reduce the size
                        maxSize = testSize - 0.1f;
                    }
                }
            }

            return optimalSize - 2;
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
                    boundingBox.Y + yOffset - 3,
                    boundingBox.Width,
                    textSize.Height
                );

                using (StringFormat sf = new StringFormat()) {
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Near;
                    sf.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                    sf.Trimming = StringTrimming.None;

                    path.AddString(text, font.FontFamily, (int)font.Style, font.Size, textRect, sf);
                }

                using (Pen outlinePen = new Pen(DarkenColor(bgColor, 40), 1.5f) { LineJoin = LineJoin.Round }) {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawPath(outlinePen, path);
                }
                
                using (SolidBrush textBrush = new SolidBrush(textColor)) {
                    g.FillPath(textBrush, path);
                }
            }
        }

        private static Color DarkenColor(Color color, int amount) {
            int r = Math.Max(0, Math.Min(255, color.R - amount));
            int g = Math.Max(0, Math.Min(255, color.G - amount));
            int b = Math.Max(0, Math.Min(255, color.B - amount));

            return Color.FromArgb(color.A, r, g, b);
        }

        private static Color LightenColor(Color color, float amount) {
            amount = Math.Min(1.0f, Math.Max(0.0f, amount));

            int r = Math.Min(255, (int)(color.R + color.R * amount));
            int g = Math.Min(255, (int)(color.G + color.G * amount));
            int b = Math.Min(255, (int)(color.B + color.B * amount));

            return Color.FromArgb(color.A, r, g, b);
        }

        /// <summary> Draws bounding box for the given translated texts </summary>
        public static Bitmap DrawBoundingBoxes(Bitmap baseImage, List<Processing.TranslatedText> translatedTexts) {
            Bitmap boxesLayer = new Bitmap(baseImage.Width, baseImage.Height);
            using (Graphics g = Graphics.FromImage(boxesLayer)) {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                foreach (var text in translatedTexts) {
                    if (!IsValidBoundingBox(text.BoundingBox)) continue;
                    Rectangle adjustedBox = text.BoundingBox;
                    adjustedBox.Height += 4;
                    adjustedBox.Y -= 2;
                    adjustedBox.Width += 2;
                    adjustedBox.X -= 1;
                    using (SolidBrush bgBrush = new SolidBrush(text.BackgroundColor)) {
                        g.FillRectangle(bgBrush, adjustedBox);
                    }
                }
            }
            return boxesLayer;
        }

        /// <summary> Draws translated text layer </summary>
        public static Bitmap DrawTranslatedTextLayer(Bitmap baseImage, List<Processing.TranslatedText> translatedTexts) {
            Bitmap textLayer = new Bitmap(baseImage.Width, baseImage.Height);
            using (Graphics g = Graphics.FromImage(textLayer)) {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                foreach (var text in translatedTexts) {
                    if (!IsValidBoundingBox(text.BoundingBox)) continue;
                    DrawTextOnly(g, text.TranslatedTextValue ?? "", text.BoundingBox, text.TextColor);
                }
            }
            return textLayer;
        }

        /// <summary> Draws text only </summary>
        private static void DrawTextOnly(Graphics g, string text, Rectangle boundingBox, Color textColor) {
            if (string.IsNullOrWhiteSpace(text)) return;
            float fontSize = CalculateOptimalFontSize(g, text, boundingBox);

            using (Font font = LoadCustomFont(fontSize + 8, FontStyle.Regular)) {
                SizeF textSize = g.MeasureString(text, font);
                float yOffset = ((boundingBox.Height - textSize.Height) / 2) + (4.5f + 3f); // Center vertically
                // no x offset, text aligns based on bounding box width
                RectangleF textRect = new RectangleF(boundingBox.X, boundingBox.Y + yOffset - 6, boundingBox.Width, textSize.Height);

                using (GraphicsPath path = new GraphicsPath()) {
                    using (StringFormat sf = new StringFormat() {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Near,
                        FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip,
                        Trimming = StringTrimming.None
                    }) {
                        path.AddString(text, font.FontFamily, (int)font.Style, font.Size, textRect, sf);
                    }
                    using (Pen outlinePen = new Pen(DarkenColor(textColor, 40), 1.5f) { LineJoin = LineJoin.Round }) {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.DrawPath(outlinePen, path);
                    }
                    using (SolidBrush textBrush = new SolidBrush(textColor)) {
                        g.FillPath(textBrush, path);
                    }
                }
            }
        }

        /// <summary> Draws the final overlay </summary>
        public static Bitmap DrawFinalOverlay(Bitmap baseImage, List<Processing.TranslatedText> translatedTexts) {
            Bitmap boxesLayer = DrawBoundingBoxes(baseImage, translatedTexts);
            Bitmap textLayer = DrawTranslatedTextLayer(baseImage, translatedTexts);
            Bitmap finalOverlay = new Bitmap(baseImage.Width, baseImage.Height);
            using (Graphics g = Graphics.FromImage(finalOverlay)) {
                // Draw the base image.
                g.DrawImage(baseImage, 0, 0, baseImage.Width, baseImage.Height);
                // Draw the bounding boxes layer (below the overlay).
                g.DrawImage(boxesLayer, 0, 0, boxesLayer.Width, boxesLayer.Height);
                // Draw the semi-transparent black overlay.
                using (SolidBrush overlayBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) {
                    g.FillRectangle(overlayBrush, 0, 0, baseImage.Width, baseImage.Height);
                }
                // Draw the translated text layer (above the overlay).
                g.DrawImage(textLayer, 0, 0, textLayer.Width, textLayer.Height);
            }
            boxesLayer.Dispose();
            textLayer.Dispose();
            return finalOverlay;
        }
    }
}