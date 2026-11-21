using iTextSharp.text;
using iTextSharp.text.pdf;

namespace PMS.web.Reporting
{
    public sealed class AttendancePrint
    {
        public string FileName { get; init; } = "تقرير-الحضور-والانصراف.pdf";
        public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string?[]> Rows { get; init; } = Array.Empty<string?[]>();
        public float[] Widths { get; init; } = Array.Empty<float>();
    }

    public static class AttendancePdf
    {
        public static byte[] Build(string webRoot, AttendancePrint report)
        {
            using var stream = new MemoryStream();
            var document = new Document(PageSize.A4, 32, 32, 32, 32);
            var writer = PdfWriter.GetInstance(document, stream);
            writer.CloseStream = false;
            document.Open();

            var regular = BaseFont.CreateFont(ResolveFont(webRoot, bold: false), BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            var bold = BaseFont.CreateFont(ResolveFont(webRoot, bold: true), BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            var title = new Font(bold, 16);
            var meta = new Font(regular, 12);
            var head = new Font(bold, 11);
            var cell = new Font(regular, 10);

            var banner = new PdfPTable(1)
            {
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                WidthPercentage = 100
            };
            banner.AddCell(Plain("الشركة الليبية للحديد والصلب", title));
            banner.AddCell(Plain("إدارة الشؤون الأمنية", meta));
            banner.AddCell(Plain("تقرير الحضور والانصراف", title));
            foreach (var line in report.Lines)
                banner.AddCell(Plain(line, meta));
            document.Add(banner);
            document.Add(new Paragraph(" "));

            var columns = report.Columns.Count == 0 ? 1 : report.Columns.Count;
            var table = new PdfPTable(columns)
            {
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                WidthPercentage = 100,
                HeaderRows = 1
            };
            if (report.Widths.Length == columns)
                table.SetWidths(report.Widths);

            foreach (var label in report.Columns)
                table.AddCell(Box(label, head, true));
            foreach (var row in report.Rows)
            {
                for (var i = 0; i < columns; i++)
                    table.AddCell(Box(i < row.Length ? row[i] : "", cell, false));
            }

            document.Add(table);
            document.Close();
            return stream.ToArray();
        }

        private static PdfPCell Plain(string text, Font font) => new(new Phrase(text, font))
        {
            Border = PdfPCell.NO_BORDER,
            HorizontalAlignment = Element.ALIGN_CENTER,
            RunDirection = PdfWriter.RUN_DIRECTION_RTL,
            PaddingBottom = 3
        };

        private static PdfPCell Box(string? text, Font font, bool header)
        {
            var value = string.IsNullOrWhiteSpace(text) ? "—" : text.Trim();
            return new PdfPCell(new Phrase(value, font))
            {
                HorizontalAlignment = Element.ALIGN_CENTER,
                VerticalAlignment = Element.ALIGN_MIDDLE,
                RunDirection = PdfWriter.RUN_DIRECTION_RTL,
                Padding = 6,
                BackgroundColor = header ? new BaseColor(228, 231, 236) : BaseColor.White
            };
        }

        private static string ResolveFont(string webRoot, bool bold)
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var preferred = bold
                ? new[] { "arialbd.ttf", "tahomabd.ttf", "arial.ttf", "tahoma.ttf" }
                : new[] { "arial.ttf", "tahoma.ttf", "arialbd.ttf" };
            foreach (var name in preferred)
            {
                var path = Path.Combine(folder, name);
                if (File.Exists(path))
                    return path;
            }

            var amiri = Path.Combine(webRoot, "fonts", "Amiri-Regular.ttf");
            return File.Exists(amiri) ? amiri : Path.Combine(folder, "arial.ttf");
        }
    }
}
