using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Media.TextFormatting;

// TODO bullet points not getting detected correctly -> incorrect line splitting and merging

namespace LangVision {
    internal static class OverlayRenderer {
        private const float MIN_FONT_SIZE = 1.0f;
        private const float MAX_FONT_SIZE = 72.0f;
        private const float WIDTH_MARGIN = 0.98f; // Allow text to use 98% of the box width
        private const float HEIGHT_MARGIN = 0.90f; // Allow text to use 90% of the box height
        private const float BLOCK_FONT_CONSISTENCY_RATIO = 0.85f; // How consistent fonts should be in a block


        private static bool IsValidBoundingBox(Rectangle box) {
            return box.Width > 0 && box.Height > 0 &&
                   box.Width < 10000 && box.Height < 10000;
        }

        /// <summary>
        /// Calculates the optimal font size to fit text within the given bounds
        /// </summary>
        private static float CalculateOptimalFontSize(Graphics g, string text, Rectangle bounds) {
            if (string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
                return MIN_FONT_SIZE;

            // Define effective bounds with margins
            float effectiveWidth = bounds.Width * WIDTH_MARGIN;
            float effectiveHeight = bounds.Height * HEIGHT_MARGIN;

            // Define a minimum and maximum font size
            float minSize = MIN_FONT_SIZE;
            float maxSize = Math.Min(MAX_FONT_SIZE, bounds.Height);

            // Start with minimum size
            float optimalSize = minSize;

            // Binary search loop: iterate until the difference between max and min is small
            while (maxSize - minSize > 0.5f) {
                float testSize = (minSize + maxSize) / 2;
                using (var testFont = LoadCustomFont(testSize, FontStyle.Regular)) {
                    // Measure the text dimensions with the test font
                    SizeF textSize = g.MeasureString(text, testFont);

                    // Check if the text fits within both width and height constraints
                    if (textSize.Width <= effectiveWidth && textSize.Height <= effectiveHeight) {
                        // Text fits, try a larger size
                        optimalSize = testSize;
                        minSize = testSize + 0.1f;
                    } else {
                        // Text doesn't fit, try a smaller size
                        maxSize = testSize - 0.1f;
                    }
                }
            }

            return optimalSize + 2;
        }

        private static Font LoadCustomFont(float fontSize, FontStyle fontStyle) {
            string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Fonts", "ProductSans.ttf");

            PrivateFontCollection pfc = new PrivateFontCollection();
            pfc.AddFontFile(fontPath);

            return new Font(pfc.Families[0], fontSize, fontStyle, GraphicsUnit.Pixel);
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

        /// <summary>
        /// Draws bounding box for the given translated texts
        /// </summary>
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

        /// <summary>
        /// Draws translated text layer with consistent font sizes within blocks
        /// </summary>
        public static Bitmap DrawTranslatedTextLayer(Bitmap baseImage, List<Processing.TranslatedText> translatedTexts) {
            Bitmap textLayer = new Bitmap(baseImage.Width, baseImage.Height);
            using (Graphics g = Graphics.FromImage(textLayer)) {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // Group texts by block ID for consistent font sizing
                var textsByBlock = translatedTexts
                    .Where(t => IsValidBoundingBox(t.BoundingBox))
                    .GroupBy(t => t.BlockId)
                    .ToList();

                foreach (var block in textsByBlock) {
                    // Calculate optimal font size for each line in the block
                    var linesWithOptimalFontSizes = block
                        .Select(text => new {
                            Text = text,
                            OptimalFontSize = CalculateOptimalFontSize(g, text.TranslatedTextValue ?? "", text.BoundingBox)
                        })
                        .ToList();

                    // Find the minimum font size that would work across all lines in the block
                    // But don't let the smallest dictate completely - use a weighted approach
                    float minFontSize = linesWithOptimalFontSizes.Min(x => x.OptimalFontSize);
                    float maxFontSize = linesWithOptimalFontSizes.Max(x => x.OptimalFontSize);

                    // Use a blend between min and midpoint to avoid too small fonts
                    float consistentFontSize = minFontSize;
                    if (linesWithOptimalFontSizes.Count > 1) {
                        float midpoint = (minFontSize + maxFontSize) / 2;
                        consistentFontSize = minFontSize * BLOCK_FONT_CONSISTENCY_RATIO +
                                            midpoint * (1 - BLOCK_FONT_CONSISTENCY_RATIO);
                    }

                    // Render each line with the consistent font size
                    foreach (var line in linesWithOptimalFontSizes) {
                        DrawTextOnly(g, line.Text.TranslatedTextValue ?? "", line.Text.BoundingBox,
                                   line.Text.TextColor, consistentFontSize);
                    }
                }
            }
            return textLayer;
        }



        /// <summary>
        /// Draws text only with specified font size
        /// </summary>
        private static void DrawTextOnly(Graphics g, string text, Rectangle boundingBox, Color textColor, float fontSize = 0) {
            if (string.IsNullOrWhiteSpace(text)) return;

            // If no specific font size provided, calculate optimal
            if (fontSize <= 0) {
                fontSize = CalculateOptimalFontSize(g, text, boundingBox);
            }

            // Use the calculated font size
            using (Font font = LoadCustomFont(fontSize, FontStyle.Regular)) {
                // Measure text with the final font to position correctly
                SizeF textSize = g.MeasureString(text, font);

                // Calculate position to center text vertically and horizontally
                //float xOffset = (boundingBox.Width - textSize.Width) / 2;
                float yOffset = (boundingBox.Height - textSize.Height) / 2;

                // Create a rectangle for text positioning
                RectangleF textRect = new RectangleF(
                    boundingBox.X,
                    boundingBox.Y + yOffset,
                    textSize.Width,
                    textSize.Height
                );

                // Create path for text drawing with proper alignment
                using (GraphicsPath path = new GraphicsPath()) {
                    using (StringFormat sf = new StringFormat() {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoWrap,
                        Trimming = StringTrimming.EllipsisCharacter
                    }) {
                        // Add text to path with proper positioning
                        path.AddString(
                            text,
                            font.FontFamily,
                            (int)font.Style,
                            font.Size,
                            textRect,
                            sf
                        );
                    }

                    // Draw text outline
                    using (Pen outlinePen = new Pen(DarkenColor(textColor, 40), 1.5f) { LineJoin = LineJoin.Round }) {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.DrawPath(outlinePen, path);
                    }

                    // Fill text
                    using (SolidBrush textBrush = new SolidBrush(textColor)) {
                        g.FillPath(textBrush, path);
                    }
                }
            }
        }


        /// <summary>
        /// Draws the final overlay
        /// </summary>
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