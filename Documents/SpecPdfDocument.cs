using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SpecBridge.Models;
using System;

namespace SpecBridge.Documents;

public class SpecPdfDocument : IDocument
{
    private readonly SpecResponse _specResponse;

    public SpecPdfDocument(SpecResponse specResponse)
    {
        _specResponse = specResponse;
    }

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Margin(50);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Lato"));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(_specResponse.Title).FontSize(24).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(10).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            column.Spacing(20);
            
            column.Item().Text("Summary").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);
            column.Item().Text(_specResponse.Summary);

            if (_specResponse.UserStories?.Count > 0)
            {
                column.Item().Element(ComposeUserStories);
            }

            if (_specResponse.FunctionalRequirements?.Count > 0 || _specResponse.NonFunctionalRequirements?.Count > 0)
            {
                column.Item().Element(ComposeRequirements);
            }

            if (_specResponse.ClarifyingQuestions?.Count > 0)
            {
                column.Item().Element(ComposeQuestions);
            }

            if (_specResponse.Risks?.Count > 0)
            {
                column.Item().Element(ComposeRisks);
            }
        });
    }

    private void ComposeUserStories(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(5);
            column.Item().Text("User Stories").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);

            foreach (var story in _specResponse.UserStories)
            {
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Column(storyCol =>
                {
                    storyCol.Item().Text(text =>
                    {
                        text.Span($"{story.Id}: ").SemiBold();
                        text.Span($"As a {story.AsA}, I want {story.IWant} so that {story.SoThat}");
                    });
                    
                    if (story.AcceptanceCriteria?.Count > 0)
                    {
                        storyCol.Item().PaddingTop(5).Text("Acceptance Criteria:").SemiBold().FontSize(10);
                        foreach (var criteria in story.AcceptanceCriteria)
                        {
                            storyCol.Item().PaddingLeft(10).Text($"• {criteria}").FontSize(10);
                        }
                    }
                });
            }
        });
    }

    private void ComposeRequirements(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(10);
            column.Item().Text("Requirements").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);

            if (_specResponse.FunctionalRequirements?.Count > 0)
            {
                column.Item().Text("Functional Requirements").FontSize(14).SemiBold();
                foreach (var req in _specResponse.FunctionalRequirements)
                {
                    column.Item().PaddingBottom(5).Column(reqCol =>
                    {
                        reqCol.Item().Text(text =>
                        {
                            text.Span($"{req.Id} [{req.Priority}]: ").SemiBold();
                            text.Span(req.Description);
                        });
                        
                        if (req.AcceptanceCriteria?.Count > 0)
                        {
                            foreach (var criteria in req.AcceptanceCriteria)
                            {
                                reqCol.Item().PaddingLeft(10).Text($"• {criteria}").FontSize(10);
                            }
                        }
                    });
                }
            }

            if (_specResponse.NonFunctionalRequirements?.Count > 0)
            {
                column.Item().Text("Non-Functional Requirements").FontSize(14).SemiBold();
                foreach (var req in _specResponse.NonFunctionalRequirements)
                {
                    column.Item().PaddingBottom(2).Text(text =>
                    {
                        text.Span($"{req.Category}: ").SemiBold();
                        text.Span($"{req.Requirement} (Target: {req.Target})");
                    });
                }
            }
        });
    }

    private void ComposeQuestions(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(5);
            column.Item().Text("Clarifying Questions").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);

            foreach (var q in _specResponse.ClarifyingQuestions)
            {
                column.Item().PaddingBottom(5).Column(qCol =>
                {
                    qCol.Item().Text($"Area: {q.Area}").SemiBold();
                    qCol.Item().Text($"Q: {q.Question}");
                    qCol.Item().Text($"Impact: {q.Impact}").FontColor(Colors.Grey.Darken1).FontSize(10);
                });
            }
        });
    }

    private void ComposeRisks(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(5);
            column.Item().Text("Risks").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);

            foreach (var risk in _specResponse.Risks)
            {
                column.Item().PaddingBottom(5).Column(riskCol =>
                {
                    riskCol.Item().Text(text =>
                    {
                        text.Span($"[{risk.Severity}] ").SemiBold().FontColor(risk.Severity.ToLower() == "high" ? Colors.Red.Medium : Colors.Orange.Medium);
                        text.Span(risk.Description);
                    });
                    riskCol.Item().Text($"Mitigation: {risk.Mitigation}").FontColor(Colors.Grey.Darken1).FontSize(10);
                });
            }
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(x =>
        {
            x.Span("Page ");
            x.CurrentPageNumber();
            x.Span(" of ");
            x.TotalPages();
        });
    }
}
