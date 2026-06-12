using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TimesheetTracker.DataModel.Services.Export;

/// <summary>
/// Renders a printable A4 timesheet PDF from the same <see cref="TimesheetRow"/>
/// set the .xlsx export uses, so both formats carry identical columns and data.
/// </summary>
internal static class TimesheetPdf
{
    // Pulled from the app's design tokens (app.css) so the document matches the UI.
    private static readonly Color Accent     = Color.FromHex("#1f9d63");
    private static readonly Color AccentText = Color.FromHex("#0f3d28");
    private static readonly Color HolidayTint = Color.FromHex("#fbf0db");
    private static readonly Color Holiday    = Color.FromHex("#b5710b");
    private static readonly Color Line       = Color.FromHex("#e6eaea");
    private static readonly Color Text       = Color.FromHex("#19201e");
    private static readonly Color TextSoft   = Color.FromHex("#545d5b");
    private static readonly Color TextMute   = Color.FromHex("#8a9291");
    private static readonly Color Dark       = Color.FromHex("#161b1a");
    private static readonly Color White      = Colors.White;

    static TimesheetPdf() =>
        // QuestPDF refuses to generate a document until a license is declared.
        // JABTech qualifies for the free Community licence (annual gross revenue
        // under US$1M). Set once, here, so every render path (DI and tests) is covered.
        QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Render(IReadOnlyList<TimesheetRow> rows, ExportDocument doc)
    {
        var totalDecimal = HoursFormat.Decimal(doc.TotalMinutes, doc.DecimalPlaces);
        var totalHmm = HoursFormat.Hmm(doc.TotalMinutes);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(8.5f).FontColor(Text).FontFamily(Fonts.Calibri));

                page.Header().Element(c => Header(c, doc));
                page.Content().PaddingTop(12).Element(c => Body(c, rows, doc, totalDecimal, totalHmm));
                page.Footer().Element(Footer);
            });
        });

        return document.GeneratePdf();
    }

    private static void Header(IContainer container, ExportDocument doc) =>
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(doc.JobName).FontSize(18).Bold().FontColor(Text);
                    left.Item().Text("Timesheet").FontSize(9).FontColor(Accent).Bold().LetterSpacing(0.08f);
                });

                row.ConstantItem(260).Column(right =>
                {
                    right.Item().AlignRight().Text(doc.PeriodLabel).FontSize(11).Bold().FontColor(Text);
                    right.Item().AlignRight().Text($"{doc.EmployeeName} · {doc.State}").FontSize(9).FontColor(TextSoft);
                });
            });

            if (doc.Fields.Count > 0)
            {
                col.Item().PaddingTop(8).Row(row =>
                {
                    foreach (var f in doc.Fields)
                    {
                        row.AutoItem().PaddingRight(18).Text(t =>
                        {
                            t.Span($"{f.Name}: ").FontColor(TextMute).FontSize(8.5f);
                            t.Span(f.Value).SemiBold().FontColor(Text).FontSize(8.5f);
                        });
                    }
                });
            }

            col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Line);
        });

    private static void Body(IContainer container, IReadOnlyList<TimesheetRow> rows, ExportDocument doc,
        string totalDecimal, string totalHmm)
    {
        if (rows.Count == 0)
        {
            container.PaddingTop(60).AlignCenter().Text("No entries in this period.")
                .FontSize(11).FontColor(TextMute);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.4f);  // Date
                c.RelativeColumn(0.7f);  // Day
                c.RelativeColumn(0.8f);  // Start
                c.RelativeColumn(0.8f);  // End
                c.RelativeColumn(0.8f);  // Break
                c.RelativeColumn(1.0f);  // Decimal
                c.RelativeColumn(0.9f);  // h:mm
                c.RelativeColumn(1.1f);  // Project
                c.RelativeColumn(0.7f);  // WFH
                c.RelativeColumn(1.6f);  // Holiday
                c.RelativeColumn(2.4f);  // Notes
            });

            table.Header(header =>
            {
                Head(header.Cell(), "Date");
                Head(header.Cell(), "Day");
                Head(header.Cell(), "Start", right: true);
                Head(header.Cell(), "End", right: true);
                Head(header.Cell(), "Break", right: true);
                Head(header.Cell(), "Decimal", right: true);
                Head(header.Cell(), "h:mm", right: true);
                Head(header.Cell(), "Project");
                Head(header.Cell(), "WFH");
                Head(header.Cell(), "Holiday");
                Head(header.Cell(), "Notes");
            });

            var i = 0;
            foreach (var r in rows)
            {
                var isHoliday = !string.IsNullOrEmpty(r.PublicHoliday);
                var bg = isHoliday ? HolidayTint : i % 2 == 1 ? Color.FromHex("#fafbfb") : White;

                Cell(table.Cell(), bg).Text(r.Date).FontFamily(Fonts.Consolas);
                Cell(table.Cell(), bg).Text(Short(r.Day));
                Cell(table.Cell(), bg, right: true).Text(r.Start).FontFamily(Fonts.Consolas);
                Cell(table.Cell(), bg, right: true).Text(r.End).FontFamily(Fonts.Consolas);
                Cell(table.Cell(), bg, right: true).Text(r.BreakMinutes > 0 ? r.BreakMinutes.ToString() : "—")
                    .FontColor(TextMute).FontFamily(Fonts.Consolas);
                Cell(table.Cell(), bg, right: true).Text(r.DecimalHours.ToString("F" + doc.DecimalPlaces))
                    .Bold().FontColor(AccentText).FontFamily(Fonts.Consolas);
                Cell(table.Cell(), bg, right: true).Text(r.HoursMinutes).FontColor(TextSoft).FontFamily(Fonts.Consolas);
                Cell(table.Cell(), bg).Text(string.IsNullOrEmpty(r.ProjectCode) ? "—" : r.ProjectCode);
                Cell(table.Cell(), bg).Text(r.WorkFromHome is null ? "—" : "WFH")
                    .FontColor(r.WorkFromHome is null ? TextMute : AccentText);
                Cell(table.Cell(), bg).Text(string.IsNullOrEmpty(r.PublicHoliday) ? "—" : r.PublicHoliday)
                    .FontColor(isHoliday ? Holiday : TextMute);
                Cell(table.Cell(), bg).Text(string.IsNullOrEmpty(r.Notes) ? "—" : r.Notes).FontColor(TextSoft);
                i++;
            }

            // Period-total row, mirroring the on-screen worksheet footer.
            table.Cell().ColumnSpan(5).Background(Dark).PaddingVertical(7).PaddingHorizontal(6)
                .Text("PERIOD TOTAL").FontColor(White).Bold().FontSize(8).LetterSpacing(0.05f);
            table.Cell().Background(Dark).PaddingVertical(7).PaddingHorizontal(6).AlignRight()
                .Text(totalDecimal).FontColor(White).Bold().FontFamily(Fonts.Consolas).FontSize(10);
            table.Cell().Background(Dark).PaddingVertical(7).PaddingHorizontal(6).AlignRight()
                .Text(totalHmm).FontColor(White).Bold().FontFamily(Fonts.Consolas);
            table.Cell().ColumnSpan(4).Background(Dark);
        });
    }

    private static void Footer(IContainer container) =>
        container.PaddingTop(8).BorderTop(0.5f).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text("Generated by Timesheet Tracker").FontSize(8).FontColor(TextMute);
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.Span("Page ").FontSize(8).FontColor(TextMute);
                t.CurrentPageNumber().FontSize(8).FontColor(TextMute);
                t.Span(" / ").FontSize(8).FontColor(TextMute);
                t.TotalPages().FontSize(8).FontColor(TextMute);
            });
        });

    private static void Head(IContainer cell, string text, bool right = false)
    {
        var c = cell.Background(Accent).PaddingVertical(5).PaddingHorizontal(6);
        if (right) c = c.AlignRight();
        c.Text(text).FontColor(White).Bold().FontSize(7.5f).LetterSpacing(0.04f);
    }

    private static IContainer Cell(IContainer cell, Color bg, bool right = false)
    {
        var c = cell.Background(bg).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(4).PaddingHorizontal(6);
        return right ? c.AlignRight() : c;
    }

    private static string Short(string day) => day.Length >= 3 ? day[..3] : day;
}
