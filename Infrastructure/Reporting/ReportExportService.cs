using System.Reflection;
using System.Text;
using SalesSaaS.Application.Reporting;

namespace SalesSaaS.Infrastructure.Reporting;

public sealed class ReportExportService : IReportExportService
{
    public byte[] CreateCsv<T>(IReadOnlyList<T> rows)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var content = new StringBuilder();
        content.AppendLine(string.Join(',', properties.Select(property => Escape(property.Name))));
        foreach (var row in rows) content.AppendLine(string.Join(',', properties.Select(property => Escape(Convert.ToString(property.GetValue(row)) ?? string.Empty))));
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(content.ToString());
    }

    public ReportPdfModel CreatePdfModel(string title, IReadOnlyList<ReportPdfRow> rows) => new(title, DateTime.UtcNow, rows);

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
