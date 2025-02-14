using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Vision.V1;

namespace LangVision {
    internal class OCR {
        public class OCRResult {
            public string? Text { get; set; }
            public Rectangle BoundingBox { get; set; }
            public float Confidence { get; set; }
        }

        private const int LINE_SPACING_THRESHOLD = 5; // Pixels tolerance for considering words on the same line

        /// <summary>
        /// Runs OCR on a specific screen region and extracts text lines with their bounding boxes.
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
                        // Get all words in the block with their positions
                        var wordInfos = block.Paragraphs
                            .SelectMany(p => p.Words)
                            .Select(word => new {
                                Text = string.Concat(word.Symbols.Select(s => s.Text)),
                                BoundingBox = word.BoundingBox,
                                CenterY = word.BoundingBox.Vertices.Average(v => v.Y)
                            })
                            .OrderBy(w => w.CenterY)
                            .ThenBy(w => w.BoundingBox.Vertices.Min(v => v.X))
                            .ToList();

                        // Group words into lines based on vertical position
                        var lineGroups = new List<List<dynamic>>();
                        var currentLine = new List<dynamic>();
                        float? currentLineY = null;

                        foreach (var word in wordInfos) {
                            if (currentLineY == null) {
                                currentLineY = (float?)word.CenterY;
                                currentLine.Add(word);
                            } else if (Math.Abs(word.CenterY - currentLineY.Value) <= LINE_SPACING_THRESHOLD) {
                                currentLine.Add(word);
                            } else {
                                lineGroups.Add(new List<dynamic>(currentLine));
                                currentLine.Clear();
                                currentLine.Add(word);
                                currentLineY = (float?)word.CenterY;
                            }
                        }
                        if (currentLine.Count > 0) {
                            lineGroups.Add(currentLine);
                        }

                        // Create OCRResult for each line
                        foreach (var line in lineGroups) {
                            var allVertices = line.SelectMany<dynamic, Vertex>(w => w.BoundingBox.Vertices).ToList();

                            int x = allVertices.Min(v => v.X);
                            int y = allVertices.Min(v => v.Y);
                            int maxX = allVertices.Max(v => v.X);
                            int maxY = allVertices.Max(v => v.Y);

                            string lineText = string.Join(" ", line.Select(w => w.Text));

                            results.Add(new OCRResult {
                                Text = lineText,
                                BoundingBox = new Rectangle(x, y, maxX - x, maxY - y),
                                Confidence = block.Confidence // Using block confidence as approximation
                            });
                        }
                    }
                }
            }

            return results;
        }
    }
}