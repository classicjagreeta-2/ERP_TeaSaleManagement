using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using TeaSaleManagement.Models.Report;

namespace TeaSaleManagement.Controllers
{
    /// <summary>
    /// Enquiries &amp; Reports > Purchase Contract Register (04011700F) -- new 2026-11-16, not a VB port. One line per
    /// tea purchase contract line (T_TEA_PURCH_CONTRACT), the columns and order of the user's query. Rows: API
    /// HoPurchaseContractRegister/Register (period on the Entry Date or the Contract Date, Purchase Types of
    /// M_SALETYPE TRN_TYPE 'P', the user's units only). Outputs: Excel (the plain data rows) and HTML.
    /// </summary>
    public class PurchaseContractRegisterController : HoReportControllerBase
    {
        protected override string Screen => "PurchaseContractRegister";
        protected override string Api => "/api/HoPurchaseContractRegister/";
        protected override string Title => "Purchase Contract Register";
        protected override string[] Lookups => new[] { "GetPurchaseTypes" };

        [HttpPost]
        public async Task<ActionResult> Report(string fromDate, string toDate, string dateBy = "E", string purTypes = "All", string outputType = "Html")
        {
            if (!CanView) return Denied();
            try
            {
                if (!TryDate(fromDate, out _) || !TryDate(toDate, out _)) return Message("Enter the From / To Date (dd/mm/yyyy).");
                // Only the two outputs of the form: Excel = the plain rows (as the query), anything else = HTML.
                bool excel = ReportRenderer.ParseOutput(outputType) == ReportOutput.Excel || ReportRenderer.ParseOutput(outputType) == ReportOutput.ExcelChecklist;
                var data = await PostApi("Register", new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DateBy = (dateBy ?? "").Trim().ToUpperInvariant() == "C" ? "C" : "E",
                    PurTypes = Codes(purTypes)
                });

                var doc = ReportDocument.Create(Title, (string)data["title"], landscape: true);
                doc.HeaderLines.Add((string)data["acYear"]);
                var note = (string)data["note"];
                if (!string.IsNullOrEmpty(note)) doc.Notes.Add(note);
                if ((bool?)data["truncated"] == true) doc.Notes.Add("Only the first rows are shown; narrow the selection.");

                var s = doc.AddSection(null,
                    ReportColumn.Text("Purchase Location", 1.3), ReportColumn.Text("Purchase Type", 1.2), ReportColumn.Text("Contract No", 0.8),
                    ReportColumn.Date("Contract Date", 0.9), new ReportColumn("Entry Date", ReportAlign.Center, 1.3, "dd/MM/yyyy HH:mm:ss"),
                    ReportColumn.Text("Broker", 1.8), ReportColumn.Text("Vendor Name", 2), ReportColumn.Text("Mark", 1.3),
                    ReportColumn.Text("Inv No", 0.8), ReportColumn.Text("Grade", 1.8), ReportColumn.Number("Bag/Chest", 2, 0.8),
                    ReportColumn.Number("Qty", 2, 0.9), ReportColumn.Number("Rate", 2, 0.8), ReportColumn.Number("Amount", 2, 1.1));
                s.Sheet = "Purchase Contract";
                foreach (var r in Rows(data))
                    s.Add(Str(r, "PURCHASE_LOCATION"), Str(r, "PURCHASE_TYPE"), Str(r, "CONTRACT_NO"), Dt(r, "CONTRACT_DATE"), Dt(r, "ENTRY_DATE"),
                          Str(r, "BROKER"), Str(r, "VENDOR_NAME"), Str(r, "MARK"), Str(r, "INVNO"), Str(r, "GRADE"),
                          Dec(r, "BAG_CHEST"), Dec(r, "QTY"), Dec(r, "RATE"), Dec(r, "AMOUNT"));

                return ReportOutputResult.Send(this, doc, excel ? "ExcelChecklist" : "Html", "Purchase Contract Register");
            }
            catch (InvalidOperationException ex) { return Message(ex.Message); }
        }
    }
}
