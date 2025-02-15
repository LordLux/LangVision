using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Vision.V1;

namespace LangVision {
    internal class OCR {
        public class OCRResult {
            public string? Text { get; set; }
            public Rectangle BoundingBox { get; set; }
            public float Confidence { get; set; }
            public Color TextColor { get; set; }
            public Color BackgroundColor { get; set; }
        }

        private const int LINE_SPACING_THRESHOLD = 5; // Pixels tolerance for considering words on the same line

        /// <summary>
        /// Runs OCR on a specific screen region and extracts text lines with their bounding boxes.
        /// Also detects text color and background color.
        /// </summary>
        public static async Task<List<OCRResult>> RecognizeTextFromRegion(Bitmap capturedImage) {
            List<OCRResult> results = new List<OCRResult>();

            ImageAnnotatorClient client = await ImageAnnotatorClient.CreateAsync();

            using (var stream = new System.IO.MemoryStream()) {
                capturedImage.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;

                Google.Cloud.Vision.V1.Image image = Google.Cloud.Vision.V1.Image.FromStream(stream);
                var response = await client.DetectDocumentTextAsync(image);

                if (response != null) {
                    foreach (var block in response.Pages[0].Blocks) {
                        foreach (var paragraph in block.Paragraphs) {
                            foreach (var word in paragraph.Words) {
                                string wordText = string.Concat(word.Symbols.Select(s => s.Text));

                                var boundingBox = word.BoundingBox;
                                int x = boundingBox.Vertices.Min(v => v.X);
                                int y = boundingBox.Vertices.Min(v => v.Y);
                                int width = boundingBox.Vertices.Max(v => v.X) - x;
                                int height = boundingBox.Vertices.Max(v => v.Y) - y;
                                Rectangle textBoundingBox = new Rectangle(x, y, width, height);

                                Color detectedTextColor = GetDominantColor(capturedImage, textBoundingBox);
                                Color detectedBackgroundColor = GetBackgroundColor(capturedImage, textBoundingBox);

                                // TODO check if the colors are too similar to each other, if so, make the textColor be the exact opposite of the backgroundColor

                                results.Add(new OCRResult {
                                    Text = wordText,
                                    BoundingBox = textBoundingBox,
                                    Confidence = block.Confidence,
                                    TextColor = detectedTextColor,
                                    BackgroundColor = detectedBackgroundColor
                                });
                            }
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Extracts the dominant text color inside a given bounding box.
        /// </summary>
        private static Color GetDominantColor(Bitmap image, Rectangle region) {
            using (Bitmap croppedImage = image.Clone(region, image.PixelFormat)) {
                int width = croppedImage.Width;
                int height = croppedImage.Height;
                Dictionary<Color, int> colorCounts = new Dictionary<Color, int>();

                for (int x = 0; x < width; x++) {
                    for (int y = 0; y < height; y++) {
                        Color pixelColor = croppedImage.GetPixel(x, y);

                        // Ignore fully transparent colors for text detection
                        if (pixelColor.A == 0) continue;

                        if (colorCounts.ContainsKey(pixelColor)) {
                            colorCounts[pixelColor]++;
                        } else {
                            colorCounts[pixelColor] = 1;
                        }
                    }
                }

                return colorCounts.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;
            }
        }

        /// <summary>
        /// Estimates the background color by analyzing pixels slightly outside the bounding box.
        /// </summary>
        private static Color GetBackgroundColor(Bitmap image, Rectangle region) {
            int margin = 5; // Pixels outside the bounding box to consider as background
            Rectangle expandedRegion = new Rectangle(
                Math.Max(0, region.X - margin),
                Math.Max(0, region.Y - margin),
                Math.Min(image.Width - region.X + margin, region.Width + 2 * margin),
                Math.Min(image.Height - region.Y + margin, region.Height + 2 * margin)
            );

            return GetDominantColor(image, expandedRegion);
        }

        /// <summary>
        /// Helper function to slightly lighten a given color.
        /// </summary>
        private static Color LightenColor(Color color, int amount) {
            int r = Math.Min(255, color.R + amount);
            int g = Math.Min(255, color.G + amount);
            int b = Math.Min(255, color.B + amount);
            return Color.FromArgb(color.A, r, g, b);
        }
    }
}
