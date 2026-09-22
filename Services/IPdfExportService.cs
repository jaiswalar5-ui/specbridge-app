using SpecBridge.Models;

namespace SpecBridge.Services;

public interface IPdfExportService
{
    byte[] CreatePdf(SpecResponse specification);
}