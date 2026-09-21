using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SpecBridge.Models;

namespace SpecBridge.Services;

public sealed class PdfExportService : IPdfExportService
{
    public byte[] CreatePdf(SpecificationDocument specification)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Margin(42);
                page.DefaultTextStyle(style => style.FontSize(10).FontColor(Colors.Grey.Darken3));
                page.Header().Column(column =>
                {
                    column.Item().Text("SPECBRIDGE AI").FontSize(11).Bold().FontColor(Colors.Red.Medium);
                    column.Item().Text(specification.Title).FontSize(24).Bold().FontColor(Colors.Grey.Darken4);
                    column.Item().PaddingTop(4).Text(specification.Summary);
                });
                page.Content().PaddingTop(24).Column(column =>
                {
                    AddSection(column, "Functional requirements", specification.FunctionalRequirements.Select(item => $"{item.Id} [{item.Priority}] {item.Description}"));
                    AddSection(column, "Non-functional requirements", specification.NonFunctionalRequirements.Select(item => $"{item.Category}: {item.Requirement} (Target: {item.Target})"));
                    AddSection(column, "User stories", specification.UserStories.Select(item => $"{item.Id}: As a {item.AsA}, I want {item.IWant}, so that {item.SoThat}."));
                    AddSection(column, "Open gaps", specification.Gaps.Select(item => $"{item.Area}: {item.Question} (Impact: {item.Impact})"));
                    AddSection(column, "Risks", specification.Risks.Select(item => $"[{item.Severity}] {item.Description} Mitigation: {item.Mitigation}"));
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("SpecBridge AI  |  ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void AddSection(ColumnDescriptor column, string title, IEnumerable<string> items)
    {
        column.Item().PaddingBottom(14).Column(section =>
        {
            section.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text(title).FontSize(14).Bold();
            foreach (var item in items.DefaultIfEmpty("None identified."))
            {
                section.Item().PaddingTop(5).Text($"- {item}");
            }
        });
    }
}