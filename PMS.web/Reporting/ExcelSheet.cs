using System.Text;

namespace PMS.web.Reporting
{
    /// <summary>جدول يفتح في إكسل (SpreadsheetML) باتجاه عربي.</summary>
    public static class ExcelSheet
    {
        public static byte[] Build(string sheetName, IReadOnlyList<string> headers, IEnumerable<string?[]> rows)
        {
            var xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.Append("<?mso-application progid=\"Excel.Sheet\"?>");
            xml.Append("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" ");
            xml.Append("xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            xml.Append("<Styles>");
            xml.Append("<Style ss:ID=\"h\"><Font ss:Bold=\"1\" ss:FontName=\"Arial\" ss:Size=\"11\"/>");
            xml.Append("<Interior ss:Color=\"#E4E7EC\" ss:Pattern=\"Solid\"/>");
            xml.Append("<Alignment ss:Horizontal=\"Right\" ss:ReadingOrder=\"RightToLeft\"/></Style>");
            xml.Append("<Style ss:ID=\"c\"><Font ss:FontName=\"Arial\" ss:Size=\"11\"/>");
            xml.Append("<Alignment ss:Horizontal=\"Right\" ss:ReadingOrder=\"RightToLeft\"/></Style>");
            xml.Append("</Styles>");
            xml.Append("<Worksheet ss:Name=\"").Append(Xml(SafeName(sheetName))).Append("\" ss:RightToLeft=\"1\"><Table>");

            xml.Append("<Row>");
            foreach (var header in headers)
                Cell(xml, header, "h");
            xml.Append("</Row>");

            foreach (var row in rows)
            {
                xml.Append("<Row>");
                for (var i = 0; i < headers.Count; i++)
                {
                    var value = i < row.Length ? row[i] : "";
                    Cell(xml, string.IsNullOrWhiteSpace(value) ? "—" : value, "c");
                }
                xml.Append("</Row>");
            }

            xml.Append("</Table></Worksheet></Workbook>");
            var payload = Encoding.UTF8.GetBytes(xml.ToString());
            var bom = Encoding.UTF8.GetPreamble();
            var file = new byte[bom.Length + payload.Length];
            Buffer.BlockCopy(bom, 0, file, 0, bom.Length);
            Buffer.BlockCopy(payload, 0, file, bom.Length, payload.Length);
            return file;
        }

        private static void Cell(StringBuilder xml, string? value, string style)
        {
            xml.Append("<Cell ss:StyleID=\"").Append(style).Append("\"><Data ss:Type=\"String\">");
            xml.Append(Xml(value));
            xml.Append("</Data></Cell>");
        }

        private static string SafeName(string? name)
        {
            var text = string.IsNullOrWhiteSpace(name) ? "تقرير" : name.Trim();
            foreach (var mark in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                text = text.Replace(mark, ' ');
            return text.Length <= 31 ? text : text[..31];
        }

        private static string Xml(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            var xml = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (char.IsControl(ch) && ch is not ('\t' or '\n' or '\r'))
                    continue;
                switch (ch)
                {
                    case '&': xml.Append("&amp;"); break;
                    case '<': xml.Append("&lt;"); break;
                    case '>': xml.Append("&gt;"); break;
                    case '"': xml.Append("&quot;"); break;
                    default: xml.Append(ch); break;
                }
            }
            return xml.ToString();
        }
    }
}
