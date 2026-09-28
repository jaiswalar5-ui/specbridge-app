using SpecBridge.Models;
using SpecBridge.Documents;
using QuestPDF.Fluent;
using System.IO;

namespace SpecBridge.Services;

public sealed class PdfExportService : IPdfExportService
{
    public Stream CreatePdf(SpecResponse specification)
    {
        var document = new SpecPdfDocument(specification);
        var stream = new MemoryStream();
        document.GeneratePdf(stream);
        stream.Position = 0;
        return stream;
    }
}