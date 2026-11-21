using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Core.Common;
using Core.Models;

namespace PMS.web.Reporting
{
    public static class LaborSheetFile
    {
        public static List<LaborSheetRow> Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new InvalidOperationException("الملف فارغ.");

            List<List<string>> rows;
            if (bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B)
                rows = ReadXlsx(bytes);
            else
            {
                var text = Decode(bytes);
                if (text.Contains("Workbook", StringComparison.OrdinalIgnoreCase))
                    rows = ReadSpreadsheetMl(text);
                else
                    throw new InvalidOperationException("صيغة الملف غير مدعومة. استخدم قالب الإكسل أو احفظ الملف بصيغة xlsx.");
            }

            if (rows.Count == 0)
                throw new InvalidOperationException("صيغة الملف غير مطابقة. حمّل القالب واستخدم ترتيب أعمدته.");

            EnsureHeader(rows[0]);
            var result = new List<LaborSheetRow>();
            for (var i = 1; i < rows.Count; i++)
            {
                var cells = rows[i];
                if (cells.All(string.IsNullOrWhiteSpace))
                    continue;

                var name = Cell(cells, 0);
                if (name.Length == 0)
                    throw new InvalidOperationException("اسم العامل فارغ في الصف " + (i + 1) + ".");

                result.Add(new LaborSheetRow
                {
                    RowNumber = i + 1,
                    Name = name,
                    Nationality = EmptyToNull(Cell(cells, 1)),
                    PassportNo = EmptyToNull(Cell(cells, 2)),
                    StatusText = EmptyToNull(Cell(cells, 3)),
                    Address = EmptyToNull(Cell(cells, 4)),
                    Notes = EmptyToNull(Cell(cells, 5))
                });
            }

            if (result.Count == 0)
                throw new InvalidOperationException("الملف لا يحتوي على عمالة.");
            return result;
        }

        private static void EnsureHeader(List<string> header)
        {
            for (var i = 0; i < LaborSheet.Headers.Length; i++)
            {
                var actual = Cell(header, i);
                if (!string.Equals(actual, LaborSheet.Headers[i], StringComparison.Ordinal))
                    throw new InvalidOperationException("صيغة الملف غير مطابقة. العمود " + (i + 1) + " يجب أن يكون «" + LaborSheet.Headers[i] + "».");
            }
        }

        private static List<List<string>> ReadSpreadsheetMl(string xml)
        {
            var document = XDocument.Parse(xml);
            return document.Descendants()
                .Where(element => element.Name.LocalName == "Row")
                .Select(ReadXmlRow)
                .ToList();
        }

        private static List<string> ReadXmlRow(XElement row)
        {
            var values = new List<string>();
            var cursor = 0;
            foreach (var cell in row.Elements().Where(element => element.Name.LocalName == "Cell"))
            {
                var indexText = cell.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Index")?.Value;
                if (int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var absolute) && absolute > 0)
                {
                    while (values.Count < absolute - 1)
                        values.Add("");
                    cursor = absolute - 1;
                }

                var data = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "Data");
                var text = (data?.Value ?? "").Trim();
                while (values.Count < cursor)
                    values.Add("");
                if (values.Count == cursor)
                    values.Add(text);
                else
                    values[cursor] = text;
                cursor++;
            }

            return values;
        }

        private static List<List<string>> ReadXlsx(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var shared = ReadSharedStrings(zip);
            var sheet = zip.Entries.FirstOrDefault(entry =>
                entry.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (sheet == null)
                throw new InvalidOperationException("تعذر العثور على ورقة العمل داخل الملف.");

            using var sheetStream = sheet.Open();
            var document = XDocument.Load(sheetStream);
            var rows = new List<List<string>>();
            foreach (var row in document.Descendants().Where(element => element.Name.LocalName == "row"))
            {
                var width = 0;
                var values = new Dictionary<int, string>();
                foreach (var cell in row.Elements().Where(element => element.Name.LocalName == "c"))
                {
                    var reference = cell.Attribute("r")?.Value ?? "";
                    var column = ColumnIndex(reference);
                    if (column < 0)
                        continue;
                    values[column] = CellValue(cell, shared);
                    if (column + 1 > width)
                        width = column + 1;
                }

                var line = new List<string>(width);
                for (var i = 0; i < width; i++)
                    line.Add(values.TryGetValue(i, out var text) ? text : "");
                rows.Add(line);
            }

            return rows;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
                return new List<string>();

            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            var values = new List<string>();
            foreach (var item in document.Descendants().Where(element => element.Name.LocalName == "si"))
            {
                var text = string.Concat(item.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value));
                values.Add(text.Trim());
            }

            return values;
        }

        private static string CellValue(XElement cell, IReadOnlyList<string> shared)
        {
            var type = cell.Attribute("t")?.Value;
            if (type == "inlineStr")
                return string.Concat(cell.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value)).Trim();

            var raw = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value ?? "";
            if (type == "s" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                && index >= 0 && index < shared.Count)
                return shared[index];

            return raw.Trim();
        }

        private static int ColumnIndex(string reference)
        {
            var index = 0;
            var seen = false;
            foreach (var ch in reference)
            {
                if (!char.IsLetter(ch))
                    break;
                seen = true;
                index = (index * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
            }

            return seen ? index - 1 : -1;
        }

        private static string Decode(byte[] bytes)
        {
            var offset = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                offset = 3;
            return System.Text.Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
        }

        private static string Cell(IReadOnlyList<string> cells, int index) =>
            index < cells.Count ? cells[index].Trim() : "";

        private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;
    }
}
