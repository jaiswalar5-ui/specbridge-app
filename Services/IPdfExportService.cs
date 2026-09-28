using SpecBridge.Models;
using System.IO;

namespace SpecBridge.Services;

public interface IPdfExportService
{
    Stream CreatePdf(SpecResponse specification);
}