using System.IO;
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
