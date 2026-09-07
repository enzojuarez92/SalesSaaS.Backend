namespace SalesSaaS.Application.Reporting;

public interface IReportExportService
{
    byte[] CreateCsv<T>(IReadOnlyList<T> rows);
    ReportPdfModel CreatePdfModel(string title, IReadOnlyList<ReportPdfRow> rows);
}

public sealed record ReportPdfModel(string Title, DateTime GeneratedAtUtc, IReadOnlyList<ReportPdfRow> Rows);
public sealed record ReportPdfRow(IReadOnlyList<string> Values);
