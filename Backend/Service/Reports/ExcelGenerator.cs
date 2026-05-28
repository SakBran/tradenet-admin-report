using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using API.Model;

namespace API.Service.Reports
{
    public static class ExcelGenerator
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private const int MaxWorksheetRows = 1_048_576;
        private const int MaxDataRows = MaxWorksheetRows - 1;
        private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static async Task<byte[]> CreateWorkbookAsync<T>(
            IQueryable<T> query,
            ReportQueryRequest request,
            string worksheetName)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(request);

            var result = await ApiResult<T>.CreateAsync(
                query,
                0,
                MaxDataRows,
                request.SortColumn,
                request.SortOrder,
                request.FilterColumn,
                request.FilterQuery);

            if (result.TotalCount > MaxDataRows)
            {
                throw new InvalidOperationException(
                    $"Excel export supports up to {MaxDataRows.ToString(CultureInfo.InvariantCulture)} data rows.");
            }

            return CreateWorkbook(result.Data, worksheetName);
        }

        public static byte[] CreateWorkbook<T>(IReadOnlyList<T> rows, string worksheetName)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var properties = GetExportProperties<T>();
            using var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteTextEntry(archive, "[Content_Types].xml", ContentTypesXml);
                WriteTextEntry(archive, "_rels/.rels", PackageRelationshipsXml);
                WriteTextEntry(archive, "xl/workbook.xml", WorkbookXml(SanitizeWorksheetName(worksheetName)));
                WriteTextEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml);
                WriteTextEntry(archive, "xl/styles.xml", StylesXml);
                WriteEntry(archive, "xl/worksheets/sheet1.xml", stream => WriteWorksheet(stream, properties, rows));
            }

            return output.ToArray();
        }

        private static PropertyInfo[] GetExportProperties<T>()
        {
            return typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0)
                .ToArray();
        }

        private static void WriteWorksheet<T>(Stream stream, PropertyInfo[] properties, IReadOnlyList<T> rows)
        {
            using var writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                CloseOutput = false,
                Indent = false
            });

            writer.WriteStartDocument();
            writer.WriteStartElement("worksheet", SpreadsheetNamespace);
            writer.WriteStartElement("sheetData");

            WriteRow(writer, 1, properties.Select(property => property.Name));

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var values = properties.Select(property => property.GetValue(rows[rowIndex]));
                WriteRow(writer, rowIndex + 2, values);
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        private static void WriteRow(XmlWriter writer, int rowNumber, IEnumerable<object?> values)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));

            var columnIndex = 1;
            foreach (var value in values)
            {
                WriteCell(writer, GetCellReference(columnIndex, rowNumber), value);
                columnIndex++;
            }

            writer.WriteEndElement();
        }

        private static void WriteCell(XmlWriter writer, string cellReference, object? value)
        {
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", cellReference);

            if (value != null)
            {
                if (TryGetNumericValue(value, out var numericValue))
                {
                    writer.WriteElementString("v", numericValue);
                }
                else
                {
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is");
                    writer.WriteStartElement("t");
                    writer.WriteString(FormatValue(value));
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement();
        }

        private static bool TryGetNumericValue(object value, out string numericValue)
        {
            numericValue = string.Empty;
            var type = Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType();

            if (type == typeof(byte)
                || type == typeof(short)
                || type == typeof(int)
                || type == typeof(long)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal))
            {
                numericValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                return !string.IsNullOrEmpty(numericValue)
                    && numericValue != "NaN"
                    && numericValue != "Infinity"
                    && numericValue != "-Infinity";
            }

            return false;
        }

        private static string FormatValue(object value)
        {
            return value switch
            {
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeOnly timeOnly => timeOnly.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                bool boolean => boolean ? "TRUE" : "FALSE",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }

        private static string GetCellReference(int columnNumber, int rowNumber)
        {
            var dividend = columnNumber;
            var columnName = string.Empty;

            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }

            return columnName + rowNumber.ToString(CultureInfo.InvariantCulture);
        }

        private static string SanitizeWorksheetName(string worksheetName)
        {
            var value = string.IsNullOrWhiteSpace(worksheetName)
                ? "Report"
                : worksheetName.Trim();

            foreach (var invalidChar in new[] { '[', ']', ':', '*', '?', '/', '\\' })
            {
                value = value.Replace(invalidChar, ' ');
            }

            return value.Length > 31 ? value[..31] : value;
        }

        private static void WriteTextEntry(ZipArchive archive, string entryName, string content)
        {
            WriteEntry(archive, entryName, stream =>
            {
                using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true);
                writer.Write(content);
            });
        }

        private static void WriteEntry(ZipArchive archive, string entryName, Action<Stream> write)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            write(stream);
        }

        private static string WorkbookXml(string worksheetName) =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            $"<workbook xmlns=\"{SpreadsheetNamespace}\" xmlns:r=\"{RelationshipsNamespace}\">" +
            "<sheets>" +
            $"<sheet name=\"{System.Security.SecurityElement.Escape(worksheetName)}\" sheetId=\"1\" r:id=\"rId1\"/>" +
            "</sheets>" +
            "</workbook>";

        private const string ContentTypesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        private const string PackageRelationshipsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private const string WorkbookRelationshipsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        private const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
            "</styleSheet>";
    }
}
