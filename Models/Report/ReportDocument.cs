using System;
using System.Collections.Generic;
using System.Linq;

namespace TeaSaleManagement.Models.Report
{
    public enum ReportAlign { Left, Right, Center }

    public enum ReportRowStyle
    {
        Normal,
        /// <summary>Bold sub-heading line inside a table (e.g. "TEA MADE").</summary>
        Group,
        Subtotal,
        Total,
        /// <summary>Empty spacer line.</summary>
        Blank
    }

    /// <summary>Output formats offered by the output buttons on top of the factory report forms.</summary>
    public enum ReportOutput
    {
        /// <summary>PDF in a new tab.</summary>
        Graphics,
        Html,
        Text,
        Excel,
        /// <summary>Excel with the plain data rows only: no report headings, merged cells, colours or total lines.</summary>
        ExcelChecklist
    }

    public class ReportColumn
    {
        public string Title { get; set; }
        /// <summary>Relative width (PDF / HTML).</summary>
        public double Width { get; set; } = 1;
        public ReportAlign Align { get; set; } = ReportAlign.Left;
        /// <summary>.NET format of numbers / dates ("N2", "N0", "dd/MM/yyyy" ...); null = ToString().</summary>
        public string Format { get; set; }
        /// <summary>Width in characters = the length of the database column(s) shown. When every column of a section
        /// has it, the PDF / HTML table is sized by it (not stretched over the page) and <see cref="Width"/> is ignored.</summary>
        public int Chars { get; set; }

        public ReportColumn() { }

        public ReportColumn Size(int chars) { Chars = chars; return this; }

        public ReportColumn(string title, ReportAlign align = ReportAlign.Left, double width = 1, string format = null)
        {
            Title = title; Align = align; Width = width; Format = format;
        }

        public static ReportColumn Text(string title, double width = 1) { return new ReportColumn(title, ReportAlign.Left, width); }
        public static ReportColumn Center(string title, double width = 1) { return new ReportColumn(title, ReportAlign.Center, width); }
        public static ReportColumn Number(string title, int decimals = 2, double width = 1)
        {
            return new ReportColumn(title, ReportAlign.Right, width, "N" + decimals);
        }
        public static ReportColumn Date(string title, double width = 1) { return new ReportColumn(title, ReportAlign.Center, width, "dd/MM/yyyy"); }
    }

    /// <summary>A heading cell spanning several columns (above the column titles).</summary>
    public class ReportHeaderCell
    {
        public string Text { get; set; }
        public int ColSpan { get; set; } = 1;
        public ReportHeaderCell(string text, int colSpan = 1) { Text = text; ColSpan = colSpan; }
    }

    public class ReportRow
    {
        public object[] Values { get; set; }
        public ReportRowStyle Style { get; set; }
        /// <summary>The first value is printed over the whole width of the table (a heading line of a group).</summary>
        public bool Span { get; set; }

        public ReportRow(ReportRowStyle style, params object[] values)
        {
            Style = style; Values = values ?? new object[0];
        }
    }

    /// <summary>One table of a report: optional caption, header bands, column titles and rows.</summary>
    public class ReportSection
    {
        public string Title { get; set; }
        /// <summary>Excel: this section starts a new sheet of this name (the sections after it follow on that sheet);
        /// null = on the sheet of the section before it. The first sheet is "Report" when the first section names none.</summary>
        public string Sheet { get; set; }
        public List<ReportColumn> Columns { get; } = new List<ReportColumn>();
        /// <summary>Optional heading rows above the column titles; each row's spans must add up to the column count.</summary>
        public List<List<ReportHeaderCell>> HeaderBands { get; } = new List<List<ReportHeaderCell>>();
        public List<ReportRow> Rows { get; } = new List<ReportRow>();

        public ReportSection(string title, params ReportColumn[] columns)
        {
            Title = title;
            Columns.AddRange(columns);
        }

        /// <summary>True when every column carries its database length (<see cref="ReportColumn.Chars"/>).</summary>
        public bool Sized
        {
            get { return Columns.Count > 0 && Columns.All(c => c.Chars > 0); }
        }

        public ReportSection Band(params ReportHeaderCell[] cells)
        {
            HeaderBands.Add(cells.ToList());
            return this;
        }

        public ReportSection Add(params object[] values) { Rows.Add(new ReportRow(ReportRowStyle.Normal, values)); return this; }
        public ReportSection AddGroup(params object[] values) { Rows.Add(new ReportRow(ReportRowStyle.Group, values)); return this; }
        /// <summary>Group line whose text runs over all the columns (e.g. the despatch no, party ... of the lines below).</summary>
        public ReportSection AddGroupLine(string text) { Rows.Add(new ReportRow(ReportRowStyle.Group, text) { Span = true }); return this; }
        public ReportSection AddSubtotal(params object[] values) { Rows.Add(new ReportRow(ReportRowStyle.Subtotal, values)); return this; }
        public ReportSection AddTotal(params object[] values) { Rows.Add(new ReportRow(ReportRowStyle.Total, values)); return this; }
        public ReportSection AddBlank() { Rows.Add(new ReportRow(ReportRowStyle.Blank)); return this; }
    }

    /// <summary>
    /// Format neutral content of a factory report. The builders fill it once;
    /// <see cref="ReportRenderer"/> turns it into PDF (graphics), HTML, text or Excel.
    /// </summary>
    public class ReportDocument
    {
        /// <summary>Group company (first heading line).</summary>
        public string Header1 { get; set; }
        /// <summary>Company / location (second heading line).</summary>
        public string Header2 { get; set; }
        /// <summary>Small lines under the second heading line (address, GSTIN ... of the garden); PDF / HTML only.</summary>
        public List<string> HeaderLines { get; } = new List<string>();
        /// <summary>Back colour of the column titles in PDF / HTML ("#RRGGBB"); null = the standard slate.</summary>
        public string HeadColor { get; set; }
        /// <summary>Font of the report in every output - PDF, HTML and Excel ("Calibri", every Bought Leaf report). The PDF
        /// falls back to Helvetica when the font is not installed on the server (Windows Fonts folder).</summary>
        public string FontName { get; set; } = ReportDocument.ReportFont;
        /// <summary>PDF / HTML: a column title is aligned like its values (text left, numbers right, dates centred);
        /// false = every title centred.</summary>
        public bool AlignTitles { get; set; }
        public string Title { get; set; }
        /// <summary>Period / selection line under the title.</summary>
        public string SubTitle { get; set; }
        public string Footer { get; set; }
        public bool Landscape { get; set; }
        public List<ReportSection> Sections { get; } = new List<ReportSection>();
        /// <summary>Free text lines printed between the heading and the first table (the text of a letter); an empty
        /// line gives a blank line.</summary>
        public List<string> Intro { get; } = new List<string>();
        /// <summary>Free text lines printed after the tables.</summary>
        public List<string> Notes { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public ReportDocument() { }

        /// <summary>Font of every report, in every output (Graphics, HTML, Excel, Excel Checklist).</summary>
        public const string ReportFont = "Calibri";

        /// <summary>Back colour of the column titles: the slate of the application's own buttons / title bars.</summary>
        public const string Slate = "#404E67";

        /// <summary>
        /// A report with the standard heading: first line = the company selected at login, second line = its
        /// location, then the report title; Calibri, column titles on slate and aligned like their values.
        /// </summary>
        public static ReportDocument Create(string title, string subTitle = null, bool landscape = false)
        {
            var user = TeaSaleManagement.Utility.SessionHelper.GetUser();
            DateTime now = DateTime.Now;
            return new ReportDocument
            {
                Header1 = ((user == null ? null : user.CurentCompany) ?? "").Trim(),
                Header2 = ((user == null ? null : user.CurentLocation) ?? "").Trim(),
                Title = title,
                SubTitle = subTitle,
                Landscape = landscape,
                HeadColor = Slate,
                AlignTitles = true,
                Footer = "Prepared by (Classic ERP) - " + title + " - " + now.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture)
                    + " - " + now.ToString("H:mm", System.Globalization.CultureInfo.InvariantCulture) + " - " + ((user == null ? null : user.getUserName) ?? "")
            };
        }
        public ReportSection AddSection(string title, params ReportColumn[] columns)
        {
            var s = new ReportSection(title, columns);
            Sections.Add(s);
            return s;
        }

        /// <summary>True when no section holds a data row (Normal / Subtotal / Total).</summary>
        public bool IsEmpty
        {
            get { return !Sections.Any(s => s.Rows.Any(r => r.Style == ReportRowStyle.Normal)); }
        }
    }
}
