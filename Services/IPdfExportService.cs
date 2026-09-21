using SpecBridge.Models;

namespace SpecBridge.Services;

public interface IPdfExportService
{
    byte[] CreatePdf(SpecificationDocument specification);
}