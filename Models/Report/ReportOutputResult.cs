using System;
using System.Web;
using System.Web.Mvc;

namespace TeaSaleManagement.Models.Report
{
    /// <summary>
    /// Sends a <see cref="ReportDocument"/> in the format of the output button clicked on a form:
    /// Graphics = PDF in the tab, Html = page, Excel = .xlsx download, ExcelChecklist = .xlsx with the
    /// plain data rows only. Usage in a controller action: <c>return ReportOutputResult.Send(this, doc, outputType, "Rent Bill");</c>
    /// </summary>
    public static class ReportOutputResult
    {
        private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public static ActionResult Send(Controller c, ReportDocument doc, string outputType, string fileName)
        {
            if (doc == null || doc.IsEmpty)
                return Message(fileName, "No record found for the selected criteria.");

            string stamp = fileName + " " + DateTime.Now.ToString("ddMMMyyyy_HHmm");
            var response = c.Response;
            switch (ReportRenderer.ParseOutput(outputType))
            {
                case ReportOutput.Html:
                    return new ContentResult { Content = ReportRenderer.ToHtml(doc), ContentType = "text/html", ContentEncoding = System.Text.Encoding.UTF8 };
                case ReportOutput.Text:
                    response.AddHeader("Content-Disposition", "inline; filename=\"" + stamp + ".txt\"");
                    return new ContentResult { Content = ReportRenderer.ToText(doc), ContentType = "text/plain", ContentEncoding = System.Text.Encoding.UTF8 };
                case ReportOutput.Excel:
                    response.AddHeader("Content-Disposition", "attachment; filename=\"" + stamp + ".xlsx\"");
                    return new FileContentResult(ReportRenderer.ToExcel(doc), Xlsx);
                case ReportOutput.ExcelChecklist:
                    response.AddHeader("Content-Disposition", "attachment; filename=\"" + stamp + " Checklist.xlsx\"");
                    return new FileContentResult(ReportRenderer.ToExcel(doc, true), Xlsx);
                default:
                    response.AddHeader("Content-Disposition", "inline; filename=\"" + stamp + ".pdf\"");
                    return new FileContentResult(ReportRenderer.ToPdf(doc), "application/pdf");
            }
        }

        /// <summary>Message page shown in the report tab (no rows, a refused request, an API error).</summary>
        public static ActionResult Message(string title, string message)
        {
            string html = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + HttpUtility.HtmlEncode(title) + "</title></head>"
                + "<body style=\"font-family:Calibri,Arial,sans-serif;background:#f4f4f4\">"
                + "<div style=\"margin:60px auto;max-width:520px;background:#fff;padding:24px;border-left:4px solid #404E67;box-shadow:0 1px 4px rgba(0,0,0,.2)\">"
                + "<h3 style=\"margin-top:0;color:#404E67\">" + HttpUtility.HtmlEncode(title) + "</h3>"
                + "<div>" + HttpUtility.HtmlEncode(message).Replace("\n", "<br>") + "</div></div></body></html>";
            return new ContentResult { Content = html, ContentType = "text/html" };
        }
    }
}
