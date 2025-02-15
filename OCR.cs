using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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

        public class OCRBlock {
            public List<OCRLine> Lines { get; set; } = new List<OCRLine>();
        }

        public class OCRLine {
            public string LineText { get; set; } = "";
            public Rectangle BoundingBox { get; set; }
            public Color BackgroundColor { get; set; }
            public List<OCRResult> Words { get; set; } = new List<OCRResult>();
        }

        private const int LINE_SPACING_THRESHOLD = 5; // Pixels tolerance for considering words on the same line

        /// <summary>
        /// Runs OCR on a specific screen region and extracts words with their bounding boxes.
        /// The text color is determined individually for each word, while the background color is computed per line.
        /// </summary>
        public static async Task<List<OCRBlock>> RecognizeTextBlocksFromRegion(Bitmap capturedImage) {
            List<OCRBlock> ocrBlocks = new List<OCRBlock>();

            ImageAnnotatorClient client = await ImageAnnotatorClient.CreateAsync();

            using (var stream = new MemoryStream()) {
                capturedImage.Save(stream, ImageFormat.Png);
                stream.Position = 0;

                Google.Cloud.Vision.V1.Image image = Google.Cloud.Vision.V1.Image.FromStream(stream);
                var response = await client.DetectDocumentTextAsync(image);

                if (response != null && response.Pages.Count > 0) {
                    // Process each block separately.
                    foreach (var page in response.Pages) {
                        foreach (var block in page.Blocks) {
                            // For this block, gather all words (from all paragraphs) into a temporary list.
                            List<(string wordText, Rectangle boundingBox, float confidence, int y, Color textColor)> blockWords =
                                new List<(string, Rectangle, float, int, Color)>();

                            foreach (var paragraph in block.Paragraphs) {
                                foreach (var word in paragraph.Words) {
                                    string wordText = string.Concat(word.Symbols.Select(s => s.Text));
                                    int x = word.BoundingBox.Vertices.Min(v => v.X);
                                    int y = word.BoundingBox.Vertices.Min(v => v.Y);
                                    int width = word.BoundingBox.Vertices.Max(v => v.X) - x;
                                    int height = word.BoundingBox.Vertices.Max(v => v.Y) - y;
                                    Rectangle wordBoundingBox = new Rectangle(x, y, width, height);

                                    // Compute the word text color individually.
                                    // We use a dummy background here; it will be recalculated per line.
                                    Color dummyBackground = Color.Black;
                                    Color wordTextColor = GetWordTextColor(capturedImage, wordBoundingBox, dummyBackground);

                                    blockWords.Add((wordText, wordBoundingBox, block.Confidence, y, wordTextColor));
                                }
                            }

                            // Group words in this block into lines based on their Y coordinate.
                            const int LINE_SPACING_THRESHOLD = 5;
                            blockWords = blockWords.OrderBy(w => w.y).ToList();
                            List<List<(string wordText, Rectangle boundingBox, float confidence, int y, Color textColor)>> lineGroups =
                                new List<List<(string, Rectangle, float, int, Color)>>();

                            foreach (var word in blockWords) {
                                if (lineGroups.Count == 0) {
                                    lineGroups.Add(new List<(string, Rectangle, float, int, Color)> { word });
                                } else {
                                    var currentLine = lineGroups.Last();
                                    int baseY = currentLine.First().y;
                                    if (Math.Abs(word.y - baseY) <= LINE_SPACING_THRESHOLD)
                                        currentLine.Add(word);
                                    else
                                        lineGroups.Add(new List<(string, Rectangle, float, int, Color)> { word });
                                }
                            }

                            // For each line in the block, create an OCRLine.
                            List<OCRLine> blockOCRLines = new List<OCRLine>();
                            foreach (var line in lineGroups) {
                                var sortedLine = line.OrderBy(w => w.boundingBox.X).ToList();
                                string combinedText = string.Join(" ", sortedLine.Select(w => w.wordText));

                                int minX = sortedLine.Min(w => w.boundingBox.X);
                                int minY = sortedLine.Min(w => w.boundingBox.Y);
                                int maxX = sortedLine.Max(w => w.boundingBox.X + w.boundingBox.Width);
                                int maxY = sortedLine.Max(w => w.boundingBox.Y + w.boundingBox.Height);
                                Rectangle lineBoundingBox = new Rectangle(minX, minY, maxX - minX, maxY - minY);

                                // Compute the background color for the line.
                                Color lineBackgroundColor;
                                using (Bitmap lineRegion = capturedImage.Clone(lineBoundingBox, capturedImage.PixelFormat)) {
                                    lineBackgroundColor = GetBackgroundColor(lineRegion);
                                }

                                // For each word, recalculate the text color using the computed line background.
                                List<OCRResult> wordResults = sortedLine.Select(w => new OCRResult {
                                    Text = w.wordText,
                                    BoundingBox = w.boundingBox,
                                    Confidence = w.confidence,
                                    TextColor = GetWordTextColor(capturedImage, w.boundingBox, lineBackgroundColor),
                                    BackgroundColor = lineBackgroundColor
                                }).ToList();

                                blockOCRLines.Add(new OCRLine {
                                    LineText = combinedText,
                                    BoundingBox = lineBoundingBox,
                                    BackgroundColor = lineBackgroundColor,
                                    Words = wordResults
                                });
                            }

                            // Create an OCRBlock for the current block and add its lines.
                            OCRBlock ocrBlock = new OCRBlock {
                                Lines = blockOCRLines
                            };
                            ocrBlocks.Add(ocrBlock);
                        }
                    }
                }
            }
            return ocrBlocks;
        }



        /// <summary>
        /// Computes the text color for a given word's region using the provided background color.
        /// </summary>
        /// <param name="image">The full-screen bitmap image.</param>
        /// <param name="region">The rectangle representing the bounding box of the word.</param>
        /// <param name="background">The background color determined for the word's line.</param>
        /// <returns>The detected text color for the word.</returns>
        public static Color GetWordTextColor(Bitmap image, Rectangle region, Color background) {
            using (Bitmap wordRegion = image.Clone(region, image.PixelFormat)) {
                Dictionary<Color, int> textColorHistogram = new Dictionary<Color, int>();
                int differenceThreshold = 50; // Adjust based on your images

                // Loop over every pixel in the word's region.
                for (int x = 0; x < wordRegion.Width; x++) {
                    for (int y = 0; y < wordRegion.Height; y++) {
                        Color pixelColor = wordRegion.GetPixel(x, y);
                        // Only consider pixels that differ significantly from the line's background.
                        if (IsSignificantlyDifferent(pixelColor, background, differenceThreshold)) {
                            if (textColorHistogram.ContainsKey(pixelColor))
                                textColorHistogram[pixelColor]++;
                            else
                                textColorHistogram[pixelColor] = 1;
                        }
                    }
                }

                // Choose the most frequent candidate color.
                if (textColorHistogram.Count > 0)
                    return textColorHistogram.OrderByDescending(kvp => kvp.Value).First().Key;
                else
                    return Color.White; // Fallback if no candidate pixels are found.
            }
        }

        /// <summary>
        /// Determines if a pixel's color is significantly different from the background color.
        /// </summary>
        private static bool IsSignificantlyDifferent(Color pixel, Color background, int threshold) {
            int diffR = pixel.R - background.R;
            int diffG = pixel.G - background.G;
            int diffB = pixel.B - background.B;
            double distance = Math.Sqrt(diffR * diffR + diffG * diffG + diffB * diffB);
            return distance > threshold;
        }

        /// <summary>
        /// Estimates the background color by averaging the pixels along the border of the provided bitmap.
        /// </summary>
        public static Color GetBackgroundColor(Bitmap bmp) {
            List<Color> borderPixels = new List<Color>();
            int width = bmp.Width;
            int height = bmp.Height;

            // Top and bottom rows.
            for (int x = 0; x < width; x++) {
                borderPixels.Add(bmp.GetPixel(x, 0));
                borderPixels.Add(bmp.GetPixel(x, height - 1));
            }
            // Left and right columns.
            for (int y = 0; y < height; y++) {
                borderPixels.Add(bmp.GetPixel(0, y));
                borderPixels.Add(bmp.GetPixel(width - 1, y));
            }

            int totalR = 0, totalG = 0, totalB = 0;
            foreach (var color in borderPixels) {
                totalR += color.R;
                totalG += color.G;
                totalB += color.B;
            }
            int count = borderPixels.Count;
            return Color.FromArgb(totalR / count, totalG / count, totalB / count);
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
