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
        }

        /// <summary>
        /// Runs OCR on a specific screen region and extracts only individual words.
        /// </summary>
        public static async Task<List<OCRResult>> RecognizeTextFromRegion(Rectangle region) {
            List<OCRResult> results = new List<OCRResult>();

            Bitmap capturedImage = Capture.CaptureScreenRegion(region);
            ImageAnnotatorClient client = await ImageAnnotatorClient.CreateAsync();

            using (var stream = new System.IO.MemoryStream()) {
                capturedImage.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;

                // Use explicit namespace to resolve ambiguity
                Google.Cloud.Vision.V1.Image image = Google.Cloud.Vision.V1.Image.FromStream(stream);
                var response = await client.DetectTextAsync(image);

                foreach (var annotation in response) {
                    if (!string.IsNullOrWhiteSpace(annotation.Description)) {
                        // Extract word-level bounding box
                        var vertices = annotation.BoundingPoly?.Vertices;
                        if (vertices != null && vertices.Count == 4) {
                            int x = vertices[0].X;
                            int y = vertices[0].Y;
                            int width = vertices[2].X - x;
                            int height = vertices[2].Y - y;

                            // Add only words (not full sentences)
                            if (!annotation.Description.Contains(" ")) {
                                results.Add(new OCRResult {
                                    Text = annotation.Description,
                                    BoundingBox = new Rectangle(x, y, width, height)
                                });
                            }
                        }
                    }
                }
            }
            return results;
        }
    }
}
