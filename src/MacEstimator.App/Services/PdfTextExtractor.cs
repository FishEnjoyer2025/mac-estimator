using System.IO;
using System.Text.RegularExpressions;
using PdfSharpCore.Drawing;
using SkiaSharp;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;

namespace MacEstimator.App.Services;

public class PdfTextExtractor
{
    private static readonly string TessDataPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "tessdata");

    /// <summary>
    /// Renders each PDF page to an image and runs Tesseract OCR.
    /// Falls back to PdfPig text extraction if OCR is unavailable.
    /// </summary>
    public Dictionary<int, string> ExtractText(string filePath)
    {
        var pages = new Dictionary<int, string>();

        if (Directory.Exists(TessDataPath) && File.Exists(Path.Combine(TessDataPath, "eng.traineddata")))
        {
            ExtractWithOcr(filePath, pages);
        }
        else
        {
            ExtractWithPdfPig(filePath, pages);
        }

        return pages;
    }

    public int GetPageCount(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        return document.NumberOfPages;
    }

    /// <summary>
    /// Generates a highlighted copy of the PDF with yellow overlays on matched keywords.
    /// Uses OCR to find word positions on scanned plans, then draws on the native PDF.
    /// </summary>
    public string GenerateHighlightedPdf(string sourcePdfPath, string outputPath, string[] keywords)
    {
        var keywordRegexes = keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => new Regex(Regex.Escape(k), RegexOptions.IgnoreCase))
            .ToArray();

        // Open with PdfSharpCore to draw on
        using var sharpDoc = PdfSharpCore.Pdf.IO.PdfReader.Open(sourcePdfPath, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Import);
        var outDoc = new PdfSharpCore.Pdf.PdfDocument();

        // Also open with PdfPig+Skia for OCR word positions
        using var pigDoc = UglyToad.PdfPig.PdfDocument.Open(sourcePdfPath, SkiaRenderingParsingOptions.Instance);
        pigDoc.AddSkiaPageFactory();

        var hasOcr = Directory.Exists(TessDataPath) && File.Exists(Path.Combine(TessDataPath, "eng.traineddata"));
        using var engine = hasOcr ? new TesseractEngine(TessDataPath, "eng", EngineMode.Default) : null;

        var renderScale = 2.0f;

        for (int pageNum = 1; pageNum <= sharpDoc.PageCount; pageNum++)
        {
            var importedPage = outDoc.AddPage(sharpDoc.Pages[pageNum - 1]);
            var pdfWidth = importedPage.Width.Point;
            var pdfHeight = importedPage.Height.Point;

            try
            {
                // First try PdfPig native text (for text-based PDFs)
                var pigPage = pigDoc.GetPage(pageNum);
                var nativeWords = pigPage.GetWords().ToList();

                List<(string Text, double X, double Y, double W, double H)> wordBoxes = [];

                if (nativeWords.Count > 10)
                {
                    // Use native word positions (PDF coordinates, origin bottom-left)
                    foreach (var w in nativeWords)
                    {
                        var bb = w.BoundingBox;
                        // Convert to top-left origin for PdfSharp
                        wordBoxes.Add((w.Text, bb.BottomLeft.X, pdfHeight - bb.TopRight.Y,
                            bb.TopRight.X - bb.BottomLeft.X, bb.TopRight.Y - bb.BottomLeft.Y));
                    }
                }
                else if (engine != null)
                {
                    // Scanned PDF — use OCR for word positions
                    using var skBitmap = pigDoc.GetPageAsSKBitmap(pageNum, renderScale, SKColors.White);
                    using var skImage = SKImage.FromBitmap(skBitmap);
                    using var skData = skImage.Encode(SKEncodedImageFormat.Png, 100);
                    var bytes = skData.ToArray();

                    var imgWidth = (double)skBitmap.Width;
                    var imgHeight = (double)skBitmap.Height;

                    using var pix = Pix.LoadFromMemory(bytes);
                    using var ocrPage = engine.Process(pix);
                    using var iter = ocrPage.GetIterator();

                    if (iter != null)
                    {
                        iter.Begin();
                        do
                        {
                            try
                            {
                                var word = iter.GetText(PageIteratorLevel.Word);
                                if (string.IsNullOrWhiteSpace(word)) continue;
                                if (!iter.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds)) continue;

                                // Convert OCR pixel coords to PDF points
                                var x = bounds.X1 / imgWidth * pdfWidth;
                                var y = bounds.Y1 / imgHeight * pdfHeight;
                                var w = (bounds.X2 - bounds.X1) / imgWidth * pdfWidth;
                                var h = (bounds.Y2 - bounds.Y1) / imgHeight * pdfHeight;

                                wordBoxes.Add((word.Trim(), x, y, w, h));
                            }
                            catch { }
                        } while (iter.Next(PageIteratorLevel.Word));
                    }
                }

                if (wordBoxes.Count == 0) continue;

                // Match and draw highlights
                var highlightedAreas = new List<XRect>();
                var matchCounts = new Dictionary<int, int>();

                using var gfx = XGraphics.FromPdfPage(importedPage, XGraphicsPdfPageOptions.Append);
                var brush = new XSolidBrush(XColor.FromArgb(55, 255, 230, 0));
                var pen = new XPen(XColor.FromArgb(140, 255, 180, 0), 0.75);

                for (int w = 0; w < wordBoxes.Count; w++)
                {
                    var runningText = "";
                    for (int span = 0; span < 4 && w + span < wordBoxes.Count; span++)
                    {
                        runningText += (span > 0 ? " " : "") + wordBoxes[w + span].Text;

                        for (int ki = 0; ki < keywordRegexes.Length; ki++)
                        {
                            if (!keywordRegexes[ki].IsMatch(runningText)) continue;

                            matchCounts.TryGetValue(ki, out var count);
                            if (count >= 5) continue;

                            // Compute bounding rect across span
                            double minX = double.MaxValue, minY = double.MaxValue;
                            double maxX = double.MinValue, maxY = double.MinValue;
                            for (int s = 0; s <= span; s++)
                            {
                                var wb = wordBoxes[w + s];
                                minX = Math.Min(minX, wb.X);
                                minY = Math.Min(minY, wb.Y);
                                maxX = Math.Max(maxX, wb.X + wb.W);
                                maxY = Math.Max(maxY, wb.Y + wb.H);
                            }

                            var rect = new XRect(minX - 2, minY - 1, maxX - minX + 4, maxY - minY + 2);

                            // Skip overlaps
                            bool overlaps = highlightedAreas.Any(existing => existing.IntersectsWith(rect));
                            if (overlaps) break;

                            gfx.DrawRectangle(pen, brush, rect);
                            highlightedAreas.Add(rect);
                            matchCounts[ki] = count + 1;
                            w += span;
                            goto nextWord;
                        }
                    }
                    nextWord:;
                }
            }
            catch
            {
                // Page failed — imported without highlights
            }
        }

        outDoc.Save(outputPath);
        return outputPath;
    }

    private static void ExtractWithOcr(string filePath, Dictionary<int, string> pages)
    {
        using var engine = new TesseractEngine(TessDataPath, "eng", EngineMode.Default);
        using var document = PdfDocument.Open(filePath, SkiaRenderingParsingOptions.Instance);
        document.AddSkiaPageFactory();

        for (int i = 1; i <= document.NumberOfPages; i++)
        {
            try
            {
                // Render page at 2x scale (~200 DPI) with white background
                using var skBitmap = document.GetPageAsSKBitmap(i, 2.0f, SKColors.White);
                using var skImage = SKImage.FromBitmap(skBitmap);
                using var skData = skImage.Encode(SKEncodedImageFormat.Png, 100);
                var bytes = skData.ToArray();

                // OCR the rendered image
                using var pix = Pix.LoadFromMemory(bytes);
                using var ocrPage = engine.Process(pix);
                var text = ocrPage.GetText();

                if (!string.IsNullOrWhiteSpace(text))
                    pages[i] = text;
            }
            catch
            {
                // If OCR fails for a page, try text extraction
                try
                {
                    var page = document.GetPage(i);
                    var words = page.GetWords();
                    var text = string.Join(" ", words.Select(w => w.Text));
                    if (!string.IsNullOrWhiteSpace(text))
                        pages[i] = text;
                }
                catch { }
            }
        }
    }

    private static void ExtractWithPdfPig(string filePath, Dictionary<int, string> pages)
    {
        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages())
        {
            try
            {
                var words = page.GetWords();
                var text = string.Join(" ", words.Select(w => w.Text));

                if (string.IsNullOrWhiteSpace(text))
                    text = page.Text;

                if (!string.IsNullOrWhiteSpace(text))
                    pages[page.Number] = text;
            }
            catch { }
        }
    }
}
