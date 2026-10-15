using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace TeaSaleManagement.Models.Report
{
    /// <summary>Renders a <see cref="ReportDocument"/> as PDF (graphics), HTML, plain text or Excel.</summary>
    public static class ReportRenderer
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static ReportOutput ParseOutput(string value)
        {
            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "html": return ReportOutput.Html;
                case "text": return ReportOutput.Text;
                case "txt": return ReportOutput.Text;
                case "excel": return ReportOutput.Excel;
                case "xlsx": return ReportOutput.Excel;
                case "excelchecklist": return ReportOutput.ExcelChecklist;
                case "checklist": return ReportOutput.ExcelChecklist;
                default: return ReportOutput.Graphics;
            }
        }

        // ----------------------------------------------------------------- cell text

        /// <summary>Display text of a value in a column (numbers / dates use the column format).</summary>
        public static string Format(object value, ReportColumn col)
        {
            if (value == null || value == DBNull.Value)
                return "";
            string f = col == null ? null : col.Format;
            if (value is DateTime)
                return ((DateTime)value).ToString(f ?? "dd/MM/yyyy", Inv);
            if (value is decimal) return ((decimal)value).ToString(f ?? "N2", Inv);
            if (value is double) return ((double)value).ToString(f ?? "N2", Inv);
            if (value is float) return ((float)value).ToString(f ?? "N2", Inv);
            if (value is int) return f == null ? ((int)value).ToString(Inv) : ((int)value).ToString(f, Inv);
            if (value is long) return f == null ? ((long)value).ToString(Inv) : ((long)value).ToString(f, Inv);
            if (value is short) return f == null ? ((short)value).ToString(Inv) : ((short)value).ToString(f, Inv);
            return Convert.ToString(value, Inv);
        }

        private static bool IsNumeric(object v)
        {
            return v is decimal || v is double || v is float || v is int || v is long || v is short;
        }

        private static string Cell(ReportSection s, ReportRow r, int i)
        {
            if (r.Values == null || i >= r.Values.Length) return "";
            return Format(r.Values[i], s.Columns[i]);
        }

        private static bool IsBold(ReportRowStyle st)
        {
            return st == ReportRowStyle.Group || st == ReportRowStyle.Subtotal || st == ReportRowStyle.Total;
        }

        /// <summary>Width of a column sized by its database length (<see cref="ReportColumn.Chars"/>), in em of the cell font:
        /// text is counted as capitals, numbers / dates as digits; never narrower than the column title.</summary>
        private static double CharEm(ReportColumn c)
        {
            return Math.Max(c.Chars * (c.Align == ReportAlign.Left ? 0.68 : 0.56), (c.Title ?? "").Length * 0.62);
        }

        /// <summary>"#RRGGBB" of the column title back colour (the document's, else the standard slate).</summary>
        private static string HeadHex(ReportDocument doc)
        {
            string h = (doc.HeadColor ?? "").Trim().TrimStart('#');
            int n;
            return h.Length == 6 && int.TryParse(h, NumberStyles.HexNumber, Inv, out n) ? "#" + h.ToUpperInvariant() : "#404E67";
        }

        // ----------------------------------------------------------------- HTML

        public static string ToHtml(ReportDocument doc)
        {
            // The page is laid out like the PDF (ToPdf): the same page shape, column shares and font steps (1pt = 1.4px),
            // centred in the window - not stretched over the whole browser width.
            int maxCols = doc.Sections.Count == 0 ? 1 : doc.Sections.Max(s => s.Columns.Count);
            bool landscape = doc.Landscape || maxCols > 7;
            int pageWidth = landscape ? 1112 : 766;                                   // A4 less the PDF margins
            string cellPx = (maxCols > 12 ? 9.1 : maxCols > 9 ? 10.5 : 11.9).ToString("0.#", Inv) + "px";

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>").Append(HttpUtility.HtmlEncode(doc.Title)).Append("</title>");
            sb.Append("<style>"
                + "body{font-family:Calibri,Arial,Helvetica,sans-serif;font-size:12px;color:#222;margin:16px;background:#fff}"
                + ".page{max-width:" + pageWidth + "px;margin:0 auto}"
                + ".h1{font-size:19px;font-weight:bold;text-align:center}.h2{font-size:15px;font-weight:bold;text-align:center}"
                + ".h3{font-size:14px;font-weight:bold;text-align:center}.h4{font-size:12.5px;text-align:center}"
                + ".cap{font-size:14px;font-weight:bold;margin:10px 0 4px 0}"
                + "table{border-collapse:collapse;width:100%;margin:6px 0 8px 0;table-layout:fixed;font-size:" + cellPx + "}"
                + "th{background:#404E67;color:#fff;border:1px solid #8A94A8;padding:3px 4px;overflow-wrap:anywhere}"
                + "td{border:1px solid #bbb;padding:2px 4px;overflow-wrap:anywhere}"
                + "td.r{text-align:right}td.c{text-align:center}"
                + "tr.group td{background:#e8ebf1;font-weight:bold}tr.subtotal td{font-weight:bold;background:#f3f4f7}"
                + "tr.total td{font-weight:bold;background:#dfe3ec;border-top:2px solid #404E67}"
                + "tr.blank td{border:0;height:8px}"
                + ".note{font-size:12px;margin:2px 0}.foot{margin-top:14px;font-size:11px;color:#555;border-top:1px solid #aaa;padding-top:4px}"
                + ".h5{font-size:12px;text-align:center}"
                + "th{background:" + HeadHex(doc) + "}"
                + (PdfSafeName(doc.FontName) == "" ? "" : "body{font-family:" + PdfSafeName(doc.FontName) + ",Arial,Helvetica,sans-serif}")
                + "table.sized{margin-left:auto;margin-right:auto}"
                + "@media print{body{margin:0}.noprint{display:none}}"
                + "</style></head><body><div class=\"page\">");
            sb.Append("<div class=\"h1\">").Append(HttpUtility.HtmlEncode(doc.Header1)).Append("</div>");
            sb.Append("<div class=\"h2\">").Append(HttpUtility.HtmlEncode(doc.Header2)).Append("</div>");
            foreach (var line in doc.HeaderLines)
                sb.Append("<div class=\"h5\">").Append(HttpUtility.HtmlEncode(line)).Append("</div>");
            sb.Append("<div class=\"h3\">").Append(HttpUtility.HtmlEncode(doc.Title)).Append("</div>");
            if (!string.IsNullOrWhiteSpace(doc.SubTitle))
                sb.Append("<div class=\"h4\">").Append(HttpUtility.HtmlEncode(doc.SubTitle)).Append("</div>");
            foreach (var line in doc.Intro)
                sb.Append("<div class=\"note\">").Append(string.IsNullOrWhiteSpace(line) ? "&nbsp;" : HttpUtility.HtmlEncode(line)).Append("</div>");

            foreach (var s in doc.Sections)
            {
                if (!string.IsNullOrWhiteSpace(s.Title))
                    sb.Append("<div class=\"cap\">").Append(HttpUtility.HtmlEncode(s.Title)).Append("</div>");
                // column shares as in the PDF: the database lengths of a sized section, else the relative widths
                double[] share = s.Sized ? s.Columns.Select(c => CharEm(c) + 0.9).ToArray() : s.Columns.Select(c => Math.Max(0.2, c.Width)).ToArray();
                double all = share.Sum();
                if (s.Sized)
                    // as wide as its columns (never wider than the page), centred
                    sb.Append("<table class=\"sized\" style=\"width:min(100%,").Append(all.ToString("0.##", Inv)).Append("em)\">");
                else
                    sb.Append("<table>");
                sb.Append("<colgroup>");
                foreach (double w in share)
                    sb.Append("<col style=\"width:").Append((w * 100 / all).ToString("0.###", Inv)).Append("%\">");
                sb.Append("</colgroup><thead>");
                foreach (var band in s.HeaderBands)
                {
                    sb.Append("<tr>");
                    foreach (var c in band)
                        sb.Append("<th colspan=\"").Append(c.ColSpan).Append("\">").Append(HttpUtility.HtmlEncode(c.Text)).Append("</th>");
                    sb.Append("</tr>");
                }
                sb.Append("<tr>");
                foreach (var c in s.Columns)
                    sb.Append(!doc.AlignTitles || c.Align == ReportAlign.Center ? "<th>" : c.Align == ReportAlign.Right ? "<th style=\"text-align:right\">" : "<th style=\"text-align:left\">")
                      .Append(HttpUtility.HtmlEncode(c.Title)).Append("</th>");
                sb.Append("</tr></thead><tbody>");
                foreach (var r in s.Rows)
                {
                    if (r.Style == ReportRowStyle.Blank) { sb.Append("<tr class=\"blank\"><td colspan=\"").Append(s.Columns.Count).Append("\"></td></tr>"); continue; }
                    if (r.Span)
                    {
                        sb.Append("<tr class=\"").Append(r.Style.ToString().ToLowerInvariant()).Append("\"><td colspan=\"").Append(s.Columns.Count).Append("\">")
                          .Append(HttpUtility.HtmlEncode(Cell(s, r, 0))).Append("</td></tr>");
                        continue;
                    }
                    sb.Append("<tr class=\"").Append(r.Style.ToString().ToLowerInvariant()).Append("\">");
                    for (int i = 0; i < s.Columns.Count; i++)
                    {
                        string cls = s.Columns[i].Align == ReportAlign.Right ? "r" : s.Columns[i].Align == ReportAlign.Center ? "c" : "";
                        sb.Append("<td").Append(cls == "" ? "" : " class=\"" + cls + "\"").Append(">").Append(HttpUtility.HtmlEncode(Cell(s, r, i))).Append("</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table>");
            }
            foreach (var n in doc.Notes)
                sb.Append("<div class=\"note\">").Append(string.IsNullOrWhiteSpace(n) ? "&nbsp;" : HttpUtility.HtmlEncode(n)).Append("</div>");
            sb.Append("<div class=\"foot\">").Append(HttpUtility.HtmlEncode(doc.Footer)).Append("</div>");
            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        // ----------------------------------------------------------------- text

        public static string ToText(ReportDocument doc)
        {
            var sb = new StringBuilder();
            var tables = new List<string[]>();
            int total = 0;
            foreach (var s in doc.Sections)
            {
                var w = ColumnWidths(s);
                total = Math.Max(total, w.Sum() + 3 * (w.Length - 1));
            }
            total = Math.Max(total, 60);
            sb.AppendLine(Center(doc.Header1, total));
            sb.AppendLine(Center(doc.Header2, total));
            sb.AppendLine(Center(doc.Title, total));
            if (!string.IsNullOrWhiteSpace(doc.SubTitle))
                sb.AppendLine(Center(doc.SubTitle, total));
            sb.AppendLine(new string('=', total));
            foreach (var line in doc.Intro)
                sb.AppendLine(line);

            foreach (var s in doc.Sections)
            {
                var w = ColumnWidths(s);
                int width = w.Sum() + 3 * (w.Length - 1);
                if (!string.IsNullOrWhiteSpace(s.Title))
                {
                    sb.AppendLine();
                    sb.AppendLine(s.Title);
                }
                sb.AppendLine(new string('-', width));
                foreach (var band in s.HeaderBands)
                {
                    int col = 0;
                    var parts = new List<string>();
                    foreach (var c in band)
                    {
                        int span = Math.Max(1, c.ColSpan);
                        int cw = 0;
                        for (int k = 0; k < span && col + k < w.Length; k++) cw += w[col + k];
                        cw += 3 * (span - 1);
                        parts.Add(Center(c.Text, cw));
                        col += span;
                    }
                    sb.AppendLine(string.Join(" | ", parts));
                }
                sb.AppendLine(string.Join(" | ", s.Columns.Select((c, i) => Pad(c.Title, w[i], c.Align == ReportAlign.Left ? ReportAlign.Left : ReportAlign.Center))));
                sb.AppendLine(new string('-', width));
                foreach (var r in s.Rows)
                {
                    if (r.Style == ReportRowStyle.Blank) { sb.AppendLine(); continue; }
                    if (r.Span) { sb.AppendLine(Cell(s, r, 0)); continue; }
                    if (r.Style == ReportRowStyle.Total) sb.AppendLine(new string('-', width));
                    sb.AppendLine(string.Join(" | ", s.Columns.Select((c, i) => Pad(Cell(s, r, i), w[i], c.Align))).TrimEnd());
                    if (r.Style == ReportRowStyle.Total) sb.AppendLine(new string('-', width));
                }
                sb.AppendLine(new string('-', width));
            }
            if (doc.Notes.Count > 0) sb.AppendLine();
            foreach (var n in doc.Notes)
                sb.AppendLine(n);
            sb.AppendLine();
            sb.AppendLine(doc.Footer);
            return sb.ToString();
        }

        private static int[] ColumnWidths(ReportSection s)
        {
            var w = new int[s.Columns.Count];
            for (int i = 0; i < w.Length; i++)
            {
                w[i] = (s.Columns[i].Title ?? "").Length;
                foreach (var r in s.Rows)
                    if (!r.Span) w[i] = Math.Max(w[i], Math.Min(Cell(s, r, i).Length, 60));
            }
            // header bands must fit their spans
            foreach (var band in s.HeaderBands)
            {
                int col = 0;
                foreach (var c in band)
                {
                    int span = Math.Max(1, c.ColSpan);
                    int have = 0;
                    for (int k = 0; k < span && col + k < w.Length; k++) have += w[col + k];
                    have += 3 * (span - 1);
                    int need = (c.Text ?? "").Length;
                    if (need > have && col + span - 1 < w.Length) w[col + span - 1] += need - have;
                    col += span;
                }
            }
            return w;
        }

        private static string Pad(string s, int width, ReportAlign align)
        {
            s = s ?? "";
            if (s.Length > width) s = s.Substring(0, width);
            if (align == ReportAlign.Right) return s.PadLeft(width);
            if (align == ReportAlign.Center) return Center(s, width);
            return s.PadRight(width);
        }

        private static string Center(string s, int width)
        {
            s = s ?? "";
            if (s.Length >= width) return s;
            int left = (width - s.Length) / 2;
            return (new string(' ', left) + s).PadRight(width);
        }

        // ----------------------------------------------------------------- Excel (.xlsx, no third party library)

        private class XlStyles
        {
            private readonly List<string> xfs = new List<string>();
            private readonly Dictionary<string, int> index = new Dictionary<string, int>();
            public XlStyles() { Xf(0, 0, 0, 0, null); }

            /// <summary>font: 0 normal 1 bold 2 white bold 3 title 4 heading 5 small; fill: 0 none 2 navy 3 group 4 subtotal 5 total; border: 0 none 1 thin 2 thin + top medium.</summary>
            public int Xf(int font, int fill, int border, int numFmt, string halign)
            {
                string key = font + "|" + fill + "|" + border + "|" + numFmt + "|" + halign;
                int i;
                if (index.TryGetValue(key, out i)) return i;
                string x = "<xf numFmtId=\"" + numFmt + "\" fontId=\"" + font + "\" fillId=\"" + fill + "\" borderId=\"" + border + "\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\""
                    + (halign == null ? "/>" : " applyAlignment=\"1\"><alignment horizontal=\"" + halign + "\" vertical=\"center\"/></xf>");
                xfs.Add(x);
                index[key] = xfs.Count - 1;
                return xfs.Count - 1;
            }

            public string ToXml()
            {
                var sb = new StringBuilder();
                sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
                sb.Append("<numFmts count=\"5\"><numFmt numFmtId=\"164\" formatCode=\"#,##0\"/><numFmt numFmtId=\"165\" formatCode=\"#,##0.0\"/><numFmt numFmtId=\"166\" formatCode=\"#,##0.00\"/><numFmt numFmtId=\"167\" formatCode=\"#,##0.000\"/><numFmt numFmtId=\"168\" formatCode=\"#,##0.0000\"/></numFmts>");
                sb.Append("<fonts count=\"6\">"
                    + "<font><sz val=\"10\"/><name val=\"Calibri\"/></font>"
                    + "<font><b/><sz val=\"10\"/><name val=\"Calibri\"/></font>"
                    + "<font><b/><sz val=\"10\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>"
                    + "<font><b/><sz val=\"14\"/><name val=\"Calibri\"/></font>"
                    + "<font><b/><sz val=\"12\"/><name val=\"Calibri\"/></font>"
                    + "<font><sz val=\"9\"/><name val=\"Calibri\"/></font></fonts>");
                sb.Append("<fills count=\"6\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>"
                    + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF404E67\"/></patternFill></fill>"
                    + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8EBF1\"/></patternFill></fill>"
                    + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF3F4F7\"/></patternFill></fill>"
                    + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFDFE3EC\"/></patternFill></fill></fills>");
                string side = "<left style=\"thin\"><color rgb=\"FFBBBBBB\"/></left><right style=\"thin\"><color rgb=\"FFBBBBBB\"/></right>";
                sb.Append("<borders count=\"3\"><border><left/><right/><top/><bottom/><diagonal/></border>"
                    + "<border>" + side + "<top style=\"thin\"><color rgb=\"FFBBBBBB\"/></top><bottom style=\"thin\"><color rgb=\"FFBBBBBB\"/></bottom><diagonal/></border>"
                    + "<border>" + side + "<top style=\"medium\"><color rgb=\"FF404E67\"/></top><bottom style=\"thin\"><color rgb=\"FFBBBBBB\"/></bottom><diagonal/></border></borders>");
                sb.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
                sb.Append("<cellXfs count=\"" + xfs.Count + "\">").Append(string.Concat(xfs)).Append("</cellXfs>");
                sb.Append("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
                return sb.ToString();
            }
        }

        private static string ColRef(int col, int row)
        {
            string s = "";
            for (int c = col; c > 0; c = (c - 1) / 26)
                s = (char)('A' + (c - 1) % 26) + s;
            return s + row.ToString(Inv);
        }

        private static string Xml(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                if (ch == '&') sb.Append("&amp;");
                else if (ch == '<') sb.Append("&lt;");
                else if (ch == '>') sb.Append("&gt;");
                else if (ch == '"') sb.Append("&quot;");
                else if (ch >= ' ' || ch == '\t' || ch == '\n' || ch == '\r') sb.Append(ch);
            }
            return sb.ToString();
        }

        private static int NumFmtId(string f, object v)
        {
            if (f != null && f.Length >= 2 && (f[0] == 'N' || f[0] == 'n'))
            {
                int d;
                if (int.TryParse(f.Substring(1), out d) && d >= 0 && d <= 4) return 164 + d;
            }
            return v is int || v is long || v is short ? 164 : 166;
        }

        /// <summary>Column number of a generated cell xml (r="AB12"), to order the cells of a row.</summary>
        private static int CellColIndex(string cellXml)
        {
            int i = cellXml.IndexOf("r=\"", StringComparison.Ordinal) + 3;
            int col = 0;
            while (i < cellXml.Length && char.IsLetter(cellXml[i])) { col = col * 26 + (char.ToUpperInvariant(cellXml[i]) - 'A' + 1); i++; }
            return col;
        }

        /// <summary>Name of an Excel sheet: at most 31 characters, none of \ / ? * [ ] :, not used yet ("Report" when empty).</summary>
        private static string SheetName(string wanted, IEnumerable<string> used)
        {
            string name = new string((wanted ?? "").Trim().Where(ch => "\\/?*[]:".IndexOf(ch) < 0).ToArray());
            if (name == "") name = "Report";
            if (name.Length > 31) name = name.Substring(0, 31);
            var taken = new HashSet<string>(used, StringComparer.OrdinalIgnoreCase);
            string unique = name;
            for (int i = 2; taken.Contains(unique); i++)
            {
                string tail = " " + i.ToString(Inv);
                unique = (name.Length + tail.Length > 31 ? name.Substring(0, 31 - tail.Length) : name) + tail;
            }
            return unique;
        }

        /// <param name="checklist">Excel Checklist: only the column titles and the data rows (group lines kept),
        /// so the sheet can be filtered / sorted / totalled as it is.</param>
        public static byte[] ToExcel(ReportDocument doc, bool checklist = false)
        {
            var st = new XlStyles();
            // sheets: a section that names a sheet starts it; the report headings, notes and footer go on the first one
            var sheets = new List<KeyValuePair<string, List<ReportSection>>>();
            foreach (var s in doc.Sections)
            {
                if (sheets.Count == 0 || !string.IsNullOrWhiteSpace(s.Sheet))
                    sheets.Add(new KeyValuePair<string, List<ReportSection>>(SheetName(s.Sheet, sheets.Select(x => x.Key)), new List<ReportSection>()));
                sheets[sheets.Count - 1].Value.Add(s);
            }
            if (sheets.Count == 0)
                sheets.Add(new KeyValuePair<string, List<ReportSection>>("Report", new List<ReportSection>()));

            Func<List<ReportSection>, bool, string> sheetXml = (sections, first) =>
            {
            var merges = new List<string>();
            var rowsXml = new SortedDictionary<int, List<string>>();
            int cols = Math.Max(1, sections.Count == 0 ? 1 : sections.Max(s => s.Columns.Count));
            var widths = new double[cols];
            int row = 1;

            Action<int, int, string> put = (r, c, xml) =>
            {
                List<string> l;
                if (!rowsXml.TryGetValue(r, out l)) { l = new List<string>(); rowsXml[r] = l; }
                l.Add(xml);
            };
            Action<int, int, string, int> text = (r, c, v, xf) =>
                put(r, c, "<c r=\"" + ColRef(c, r) + "\" s=\"" + xf + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + Xml(v) + "</t></is></c>");
            Action<int, int, int> blank = (r, c, xf) => put(r, c, "<c r=\"" + ColRef(c, r) + "\" s=\"" + xf + "\"/>");
            Action<int, int, int> merge = (r, c1, c2) => { if (c2 > c1) merges.Add(ColRef(c1, r) + ":" + ColRef(c2, r)); };

            Action<string, int, int> heading = (t, font, r) =>
            {
                int xf = st.Xf(font, 0, 0, 0, "center");
                text(r, 1, t ?? "", xf);
                for (int c = 2; c <= cols; c++) blank(r, c, xf);
                merge(r, 1, cols);
            };
            if (!checklist && first)
            {
                heading(doc.Header1, 3, row++);
                heading(doc.Header2, 4, row++);
                heading(doc.Title, 4, row++);
                if (!string.IsNullOrWhiteSpace(doc.SubTitle)) heading(doc.SubTitle, 0, row++);
                row++;
                foreach (var line in doc.Intro)
                {
                    if (!string.IsNullOrWhiteSpace(line)) text(row, 1, line, st.Xf(0, 0, 0, 0, null));
                    row++;
                }
                if (doc.Intro.Count > 0) row++;
            }

            foreach (var s in sections)
            {
                int n = s.Columns.Count;
                if (n == 0) continue;
                if (!string.IsNullOrWhiteSpace(s.Title)) text(row++, 1, s.Title, st.Xf(1, 0, 0, 0, null));
                int hx = st.Xf(2, 2, 1, 0, "center");
                foreach (var band in checklist ? new List<List<ReportHeaderCell>>() : s.HeaderBands)
                {
                    int col = 1;
                    foreach (var c in band)
                    {
                        int span = Math.Max(1, c.ColSpan);
                        int last = Math.Min(n, col + span - 1);
                        text(row, col, c.Text ?? "", hx);
                        for (int k = col + 1; k <= last; k++) blank(row, k, hx);
                        merge(row, col, last);
                        col += span;
                    }
                    for (; col <= n; col++) blank(row, col, hx);
                    row++;
                }
                for (int i = 0; i < n; i++)
                {
                    text(row, i + 1, s.Columns[i].Title ?? "", hx);
                    widths[i] = Math.Max(widths[i], (s.Columns[i].Title ?? "").Length);
                }
                row++;

                foreach (var r in s.Rows)
                {
                    if (checklist && r.Style != ReportRowStyle.Normal && r.Style != ReportRowStyle.Group) continue;
                    if (r.Style == ReportRowStyle.Blank) { row++; continue; }
                    int font = IsBold(r.Style) ? 1 : 0;
                    int fill = checklist ? 0 : r.Style == ReportRowStyle.Group ? 3 : r.Style == ReportRowStyle.Subtotal ? 4 : r.Style == ReportRowStyle.Total ? 5 : 0;
                    int border = r.Style == ReportRowStyle.Total ? 2 : 1;
                    if (r.Span)
                    {
                        // one text over all the columns (not merged in the checklist, so the sheet can still be filtered)
                        int sx = st.Xf(font, fill, border, 0, "left");
                        text(row, 1, Cell(s, r, 0), sx);
                        for (int k = 2; k <= n; k++) blank(row, k, sx);
                        if (!checklist) merge(row, 1, n);
                        row++;
                        continue;
                    }
                    for (int i = 0; i < n; i++)
                    {
                        object v = r.Values != null && i < r.Values.Length ? r.Values[i] : null;
                        string align = s.Columns[i].Align == ReportAlign.Right ? "right" : s.Columns[i].Align == ReportAlign.Center ? "center" : "left";
                        if (v == null || v == DBNull.Value) { blank(row, i + 1, st.Xf(font, fill, border, 0, align)); continue; }
                        if (v is DateTime)
                        {
                            widths[i] = Math.Max(widths[i], 11);
                            put(row, i + 1, "<c r=\"" + ColRef(i + 1, row) + "\" s=\"" + st.Xf(font, fill, border, 14, "center") + "\"><v>"
                                + (((DateTime)v) - new DateTime(1899, 12, 30)).TotalDays.ToString("0.######", Inv) + "</v></c>");
                        }
                        else if (IsNumeric(v))
                        {
                            widths[i] = Math.Max(widths[i], Format(v, s.Columns[i]).Length);
                            put(row, i + 1, "<c r=\"" + ColRef(i + 1, row) + "\" s=\"" + st.Xf(font, fill, border, NumFmtId(s.Columns[i].Format, v), "right") + "\"><v>"
                                + Convert.ToDouble(v, Inv).ToString("R", Inv) + "</v></c>");
                        }
                        else
                        {
                            string sv = Convert.ToString(v, Inv);
                            widths[i] = Math.Max(widths[i], Math.Min(sv.Length, 60));
                            text(row, i + 1, sv, st.Xf(font, fill, border, 0, align));
                        }
                    }
                    row++;
                }
                row++;
            }
            if (!checklist && first)
            {
                foreach (var note in doc.Notes) text(row++, 1, note, st.Xf(5, 0, 0, 0, null));
                row++;
                text(row, 1, doc.Footer ?? "", st.Xf(5, 0, 0, 0, null));
            }

            var ws = new StringBuilder();
            ws.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            ws.Append("<sheetPr><pageSetUpPr fitToPage=\"1\"/></sheetPr><sheetViews><sheetView workbookViewId=\"0\"/></sheetViews><sheetFormatPr defaultRowHeight=\"13\"/><cols>");
            for (int i = 0; i < cols; i++)
                ws.Append("<col min=\"").Append(i + 1).Append("\" max=\"").Append(i + 1).Append("\" width=\"")
                  .Append(Math.Min(Math.Max(widths[i] + 2, 9), 60).ToString("0.##", Inv)).Append("\" customWidth=\"1\"/>");
            ws.Append("</cols><sheetData>");
            foreach (var kv in rowsXml)
            {
                ws.Append("<row r=\"").Append(kv.Key).Append("\">");
                foreach (var c in kv.Value.OrderBy(x => CellColIndex(x))) ws.Append(c);
                ws.Append("</row>");
            }
            ws.Append("</sheetData>");
            if (merges.Count > 0)
            {
                ws.Append("<mergeCells count=\"").Append(merges.Count).Append("\">");
                foreach (var m in merges) ws.Append("<mergeCell ref=\"").Append(m).Append("\"/>");
                ws.Append("</mergeCells>");
            }
            ws.Append("<pageMargins left=\"0.4\" right=\"0.4\" top=\"0.5\" bottom=\"0.5\" header=\"0.3\" footer=\"0.3\"/>");
            ws.Append("<pageSetup paperSize=\"9\" orientation=\"").Append(doc.Landscape ? "landscape" : "portrait").Append("\" fitToWidth=\"1\" fitToHeight=\"0\"/></worksheet>");
            return ws.ToString();
            };

            var sheetsXml = sheets.Select((sh, i) => sheetXml(sh.Value, i == 0)).ToList();

            using (var ms = new MemoryStream())
            {
                using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    Action<string, string> add = (name, content) =>
                    {
                        var e = zip.CreateEntry(name);
                        using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(content);
                    };
                    const string hdr = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";
                    add("[Content_Types].xml", hdr + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                        + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                        + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                        + string.Concat(sheets.Select((sh, i) => "<Override PartName=\"/xl/worksheets/sheet" + (i + 1) + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"))
                        + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
                    add("_rels/.rels", hdr + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                        + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                    add("xl/workbook.xml", hdr + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                        + "<sheets>" + string.Concat(sheets.Select((sh, i) => "<sheet name=\"" + Xml(sh.Key) + "\" sheetId=\"" + (i + 1) + "\" r:id=\"rId" + (i + 1) + "\"/>"))
                        + "</sheets></workbook>");
                    add("xl/_rels/workbook.xml.rels", hdr + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                        + string.Concat(sheets.Select((sh, i) => "<Relationship Id=\"rId" + (i + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + (i + 1) + ".xml\"/>"))
                        + "<Relationship Id=\"rId" + (sheets.Count + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                    add("xl/styles.xml", st.ToXml());
                    for (int i = 0; i < sheetsXml.Count; i++)
                        add("xl/worksheets/sheet" + (i + 1) + ".xml", sheetsXml[i]);
                }
                return ms.ToArray();
            }
        }

        // ----------------------------------------------------------------- PDF (graphics)

        public static byte[] ToPdf(ReportDocument doc)
        {
            int maxCols = doc.Sections.Count == 0 ? 1 : doc.Sections.Max(s => s.Columns.Count);
            bool landscape = doc.Landscape || maxCols > 7;
            var page = landscape ? PageSize.A4.Rotate() : PageSize.A4;
            using (var ms = new MemoryStream())
            {
                var pdf = new Document(page, 24, 24, 24, 34);
                var writer = PdfWriter.GetInstance(pdf, ms);
                // the document's own font (Calibri) when it is installed, else Helvetica
                BaseFont own = PdfBase(doc.FontName, false), ownBold = own == null ? null : PdfBase(doc.FontName, true);
                if (ownBold == null) own = null;
                Func<bool, float, BaseColor, Font> font = (b, size, color) => own != null
                    ? new Font(b ? ownBold : own, size, Font.NORMAL, color ?? BaseColor.BLACK)
                    : FontFactory.GetFont(b ? FontFactory.HELVETICA_BOLD : FontFactory.HELVETICA, size, color ?? BaseColor.BLACK);
                writer.PageEvent = new FooterEvent(doc.Footer, own);
                pdf.Open();

                var f1 = font(true, 14, null);
                var f2 = font(true, 11, null);
                var f3 = font(true, 10, null);
                var f4 = font(false, 9, null);
                AddCentered(pdf, doc.Header1, f1);
                AddCentered(pdf, doc.Header2, f2);
                foreach (var line in doc.HeaderLines)
                    AddCentered(pdf, line, f4);
                AddCentered(pdf, doc.Title, f3);
                AddCentered(pdf, doc.SubTitle, f4);
                pdf.Add(new Paragraph(" ", FontFactory.GetFont(FontFactory.HELVETICA, 4)));
                foreach (var line in doc.Intro)
                    pdf.Add(new Paragraph(string.IsNullOrWhiteSpace(line) ? " " : line, f4));
                if (doc.Intro.Count > 0)
                    pdf.Add(new Paragraph(" ", FontFactory.GetFont(FontFactory.HELVETICA, 4)));

                float fontSize = maxCols > 12 ? 6.5f : maxCols > 9 ? 7.5f : 8.5f;
                var normal = font(false, fontSize, null);
                var bold = font(true, fontSize, null);
                var head = font(true, fontSize, new BaseColor(255, 255, 255));
                int rgb = int.Parse(HeadHex(doc).Substring(1), NumberStyles.HexNumber, Inv);
                var navy = new BaseColor((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
                var headLine = new BaseColor(0x8A, 0x94, 0xA8);   // lines between the title cells: light slate, not black

                foreach (var s in doc.Sections)
                {
                    int n = s.Columns.Count;
                    if (n == 0) continue;
                    if (!string.IsNullOrWhiteSpace(s.Title))
                    {
                        var cap = new Paragraph(s.Title, f3) { SpacingBefore = 6, SpacingAfter = 3 };
                        pdf.Add(cap);
                    }
                    var t = new PdfPTable(n) { WidthPercentage = 100, HeaderRows = s.HeaderBands.Count + 1 };
                    if (s.Sized)
                    {
                        // columns as wide as their database length; the table is not stretched over the page
                        float[] w = s.Columns.Select(c => (float)(CharEm(c) * fontSize) + 6f).ToArray();
                        t.SetWidths(w);
                        if (w.Sum() < pdf.Right - pdf.Left)
                        {
                            t.TotalWidth = w.Sum();
                            t.LockedWidth = true;
                            t.HorizontalAlignment = Element.ALIGN_CENTER;
                        }
                    }
                    else
                        t.SetWidths(s.Columns.Select(c => (float)Math.Max(0.2, c.Width)).ToArray());
                    t.SplitLate = false;

                    foreach (var band in s.HeaderBands)
                    {
                        int used = 0;
                        foreach (var c in band)
                        {
                            int span = Math.Max(1, Math.Min(c.ColSpan, n - used));
                            t.AddCell(new PdfPCell(new Phrase(c.Text ?? "", head)) { Colspan = span, BackgroundColor = navy, BorderColor = headLine, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 3 });
                            used += span;
                        }
                        for (; used < n; used++)
                            t.AddCell(new PdfPCell(new Phrase("", head)) { BackgroundColor = navy, BorderColor = headLine });
                    }
                    foreach (var c in s.Columns)
                        t.AddCell(new PdfPCell(new Phrase(c.Title ?? "", head))
                        {
                            BackgroundColor = navy,
                            BorderColor = headLine,
                            HorizontalAlignment = !doc.AlignTitles || c.Align == ReportAlign.Center ? Element.ALIGN_CENTER
                                : c.Align == ReportAlign.Right ? Element.ALIGN_RIGHT : Element.ALIGN_LEFT,
                            Padding = 3
                        });

                    foreach (var r in s.Rows)
                    {
                        if (r.Style == ReportRowStyle.Blank)
                        {
                            t.AddCell(new PdfPCell(new Phrase(" ", normal)) { Colspan = n, Border = Rectangle.NO_BORDER, FixedHeight = 6 });
                            continue;
                        }
                        BaseColor back = r.Style == ReportRowStyle.Group ? new BaseColor(0xE8, 0xEB, 0xF1)
                            : r.Style == ReportRowStyle.Subtotal ? new BaseColor(0xF3, 0xF4, 0xF7)
                            : r.Style == ReportRowStyle.Total ? new BaseColor(0xDF, 0xE3, 0xEC) : null;
                        if (r.Span)
                        {
                            var wide = new PdfPCell(new Phrase(Cell(s, r, 0), IsBold(r.Style) ? bold : normal))
                            {
                                Colspan = n,
                                Padding = 2.5f,
                                BorderColor = new BaseColor(0xBB, 0xBB, 0xBB)
                            };
                            if (back != null) wide.BackgroundColor = back;
                            t.AddCell(wide);
                            continue;
                        }
                        for (int i = 0; i < n; i++)
                        {
                            var cell = new PdfPCell(new Phrase(Cell(s, r, i), IsBold(r.Style) ? bold : normal))
                            {
                                HorizontalAlignment = s.Columns[i].Align == ReportAlign.Right ? Element.ALIGN_RIGHT
                                    : s.Columns[i].Align == ReportAlign.Center ? Element.ALIGN_CENTER : Element.ALIGN_LEFT,
                                Padding = 2.5f,
                                BorderColor = new BaseColor(0xBB, 0xBB, 0xBB)
                            };
                            if (back != null) cell.BackgroundColor = back;
                            t.AddCell(cell);
                        }
                    }
                    pdf.Add(t);
                }
                foreach (var note in doc.Notes)
                    pdf.Add(new Paragraph(string.IsNullOrWhiteSpace(note) ? " " : note, f4));
                if (doc.Sections.Count == 0 && doc.Notes.Count == 0)
                    pdf.Add(new Paragraph("No record found for the selected criteria.", f4));
                pdf.Close();
                return ms.ToArray();
            }
        }

        /// <summary>The document's font name when it is a plain name (letters / digits), else "".</summary>
        private static string PdfSafeName(string fontName)
        {
            string name = (fontName ?? "").Trim();
            return name.All(char.IsLetterOrDigit) ? name : "";
        }

        /// <summary>Embedded TrueType font of the Windows Fonts folder ("Calibri" -> calibri.ttf / calibrib.ttf); null when not there.</summary>
        private static BaseFont PdfBase(string fontName, bool bold)
        {
            string name = (fontName ?? "").Trim().ToLowerInvariant();
            if (name == "" || !name.All(char.IsLetterOrDigit))
                return null;
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), name + (bold ? "b" : "") + ".ttf");
                return File.Exists(path) ? BaseFont.CreateFont(path, BaseFont.CP1252, BaseFont.EMBEDDED) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AddCentered(Document pdf, string text, Font font)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            pdf.Add(new Paragraph(text, font) { Alignment = Element.ALIGN_CENTER });
        }

        private class FooterEvent : PdfPageEventHelper
        {
            private readonly string footer;
            private PdfTemplate total;
            private BaseFont bf;

            public FooterEvent(string footer, BaseFont font = null) { this.footer = footer ?? ""; bf = font; }

            public override void OnOpenDocument(PdfWriter writer, Document document)
            {
                if (bf == null)
                    bf = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, false);
                total = writer.DirectContent.CreateTemplate(30, 12);
            }

            public override void OnEndPage(PdfWriter writer, Document document)
            {
                var cb = writer.DirectContent;
                float y = document.Bottom - 14;
                cb.BeginText();
                cb.SetFontAndSize(bf, 7);
                cb.SetTextMatrix(document.Left, y);
                cb.ShowText(footer);
                cb.EndText();
                string pg = "Page " + writer.PageNumber + " of ";
                float len = bf.GetWidthPoint(pg, 7);
                float x = document.Right - len - 12;
                cb.BeginText();
                cb.SetFontAndSize(bf, 7);
                cb.SetTextMatrix(x, y);
                cb.ShowText(pg);
                cb.EndText();
                cb.AddTemplate(total, x + len, y);
            }

            public override void OnCloseDocument(PdfWriter writer, Document document)
            {
                total.BeginText();
                total.SetFontAndSize(bf, 7);
                total.SetTextMatrix(0, 0);
                total.ShowText((writer.PageNumber - 1).ToString(Inv));
                total.EndText();
            }
        }
    }
}
