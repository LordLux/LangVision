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
        
        private const int BASE_LINE_SPACING_THRESHOLD = 5; // Base threshold for small text
        private const float LINE_SPACING_RATIO = 0.4f; // Threshold as ratio of text height
        private const float OVERLAP_THRESHOLD = 0.5f; // Minimum vertical overlap ratio to consider words in same line

        /// <summary>
        /// Determines if two words should be considered part of the same line based on their
        /// vertical position, size, and overlap.
        /// </summary>
        private static bool AreWordsOnSameLine(
            (string wordText, Rectangle boundingBox, float confidence, int y, Color textColor) word1,
            (string wordText, Rectangle boundingBox, float confidence, int y, Color textColor) word2) {
            Rectangle box1 = word1.boundingBox;
            Rectangle box2 = word2.boundingBox;

            // Calculate vertical overlap
            int overlapStart = Math.Max(box1.Top, box2.Top);
            int overlapEnd = Math.Min(box1.Bottom, box2.Bottom);
            int overlapHeight = overlapEnd - overlapStart;

            // Calculate overlap ratio relative to the smaller box height
            float smallerHeight = Math.Min(box1.Height, box2.Height);
            float overlapRatio = overlapHeight / smallerHeight;

            // If there's significant vertical overlap, consider them on the same line
            if (overlapRatio >= OVERLAP_THRESHOLD) {
                return true;
            }

            // Dynamic spacing threshold based on text height
            float avgHeight = (box1.Height + box2.Height) / 2.0f;
            int dynamicThreshold = Math.Max(
                BASE_LINE_SPACING_THRESHOLD,
                (int)(avgHeight * LINE_SPACING_RATIO)
            );

            // Check vertical distance between boxes
            int verticalDistance = Math.Abs(
                (box1.Top + box1.Height / 2) - (box2.Top + box2.Height / 2)
            );

            return verticalDistance <= dynamicThreshold;
        }

        /// <summary>
        /// Groups words into lines using a more sophisticated algorithm that considers
        /// text size and vertical overlap.
        /// </summary>
        private static List<List<(string wordText, Rectangle boundingBox, float confidence, int y, Color textColor)>>
        GroupWordsIntoLines(List<(string wordText, Rectangle boundingBox, float confidence, int y, Color textColor)> words) {
            var lineGroups = new List<List<(string wordText, Rectangle boundingBox, float confidence, int y, Color textColor)>>();
            if (!words.Any()) return lineGroups;

            // Sort words by their vertical position (top to bottom)
            var sortedWords = words.OrderBy(w => w.boundingBox.Y).ToList();

            // Start with the first word in its own line
            lineGroups.Add(new List<(string, Rectangle, float, int, Color)> { sortedWords[0] });

            // Process remaining words
            for (int i = 1; i < sortedWords.Count; i++) {
                var currentWord = sortedWords[i];
                bool addedToExistingLine = false;

                // Check each existing line
                for (int lineIdx = lineGroups.Count - 1; lineIdx >= Math.Max(0, lineGroups.Count - 3); lineIdx--) {
                    var line = lineGroups[lineIdx];

                    // Check if the word belongs to this line by comparing with all words in the line
                    if (line.Any(lineWord => AreWordsOnSameLine(lineWord, currentWord))) {
                        line.Add(currentWord);
                        addedToExistingLine = true;
                        break;
                    }
                }

                // If word doesn't belong to any existing line, start a new line
                if (!addedToExistingLine) {
                    lineGroups.Add(new List<(string, Rectangle, float, int, Color)> { currentWord });
                }
            }

            // Sort words within each line by X coordinate
            for (int i = 0; i < lineGroups.Count; i++) {
                lineGroups[i] = lineGroups[i].OrderBy(w => w.boundingBox.X).ToList();
            }

            return lineGroups;
        }

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
                    foreach (var page in response.Pages) {
                        foreach (var block in page.Blocks) {
                            // Gather all words in the block
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

                                    Color dummyBackground = Color.Black;
                                    Color wordTextColor = GetWordTextColor(capturedImage, wordBoundingBox, dummyBackground);

                                    blockWords.Add((wordText, wordBoundingBox, block.Confidence, y, wordTextColor));
                                }
                            }

                            // Use the new grouping algorithm
                            var lineGroups = GroupWordsIntoLines(blockWords);

                            // Create OCR lines from the grouped words
                            List<OCRLine> blockOCRLines = new List<OCRLine>();
                            foreach (var line in lineGroups) {
                                string combinedText = string.Join(" ", line.Select(w => w.wordText));

                                int minX = line.Min(w => w.boundingBox.X);
                                int minY = line.Min(w => w.boundingBox.Y);
                                int maxX = line.Max(w => w.boundingBox.X + w.boundingBox.Width);
                                int maxY = line.Max(w => w.boundingBox.Y + w.boundingBox.Height);
                                Rectangle lineBoundingBox = new Rectangle(minX, minY, maxX - minX, maxY - minY);

                                // Compute background color for the line
                                Color lineBackgroundColor;
                                using (Bitmap lineRegion = capturedImage.Clone(lineBoundingBox, capturedImage.PixelFormat)) {
                                    lineBackgroundColor = GetBackgroundColor(lineRegion);
                                }

                                // Create word results with recalculated text colors
                                List<OCRResult> wordResults = line.Select(w => new OCRResult {
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
