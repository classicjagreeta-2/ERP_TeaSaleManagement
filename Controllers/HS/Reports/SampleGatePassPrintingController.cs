using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using TeaSaleManagement.Models.Report;

namespace TeaSaleManagement.Controllers
{
    /// <summary>
    /// Sample Tea > Enquiries &amp; Reports > Sample Gate Pass Printing (05020100T) -- old HO Sales Con_HO_03030209TController
    /// (Crystal SampleGatePass.rpt). Rows: API HoSampleGatePass/Print. One gate pass per sample-draw document: the
    /// document (Doc No / Date, Unit, Sale Centre), "To" = the party (else the broker) with address / state / GSTIN, the
    /// broker and the warehouse with its address, then the sampled lots and the document's total Draw Qty.
    /// Criteria as the old form: Doc No From / To (picked from the period's documents) or, with no Doc No, the period.
    /// </summary>
    public class SampleGatePassPrintingController : HoReportControllerBase
    {
        protected override string Screen => "SampleGatePassPrinting";
        protected override string Api => "/api/HoSampleGatePass/";
        protected override string Title => "Sample Gate Pass";
        protected override string[] Lookups => new[] { "GetDocuments" };

        [HttpPost]
        public async Task<ActionResult> Report(string fromDate, string toDate, string docNoFrom = "", string docNoTo = "", string outputType = "Graphics")
        {
            if (!CanView) return Denied();
            try
            {
                if (string.IsNullOrWhiteSpace(docNoFrom) && string.IsNullOrWhiteSpace(docNoTo) && (!TryDate(fromDate, out _) || !TryDate(toDate, out _)))
                    return Message("Enter the From / To Date (dd/mm/yyyy).");
                var data = await PostApi("Print", new { FromDate = fromDate, ToDate = toDate, DocNoFrom = docNoFrom, DocNoTo = docNoTo });
                var rows = Rows(data).ToList();
                if (rows.Count == 0) return Message("No record found.");

                var doc = ReportDocument.Create("Sample Gate Pass", (string)data["caption"]);
                var s = doc.AddSection(null,
                    ReportColumn.Number("Sl", 0, 0.3), ReportColumn.Text("Garden", 1.4), ReportColumn.Text("Mark", 1.1), ReportColumn.Text("Inv No", 0.7),
                    ReportColumn.Text("Grade", 0.8), ReportColumn.Text("Lot No", 0.6), ReportColumn.Text("Sale No", 0.6), ReportColumn.Date("Sale Date", 0.7),
                    ReportColumn.Date("Prompt Date", 0.7), ReportColumn.Text("Chest No", 0.5), ReportColumn.Number("Draw Qty (Kg)", 3, 0.8));
                object[] Total(string label, IEnumerable<JToken> l) =>
                    new object[] { null, label, "", "", "", "", "", null, null, "", l.Sum(r => Dec(r, "DRAW_QTY")) };

                foreach (var d in rows.GroupBy(r => new { Unit = Str(r, "UNIT"), No = Str(r, "DOCNO"), Dt = Dt(r, "DOCDT") }))
                {
                    var f = d.First();
                    s.AddGroupLine($"Gate Pass No : {d.Key.No}    Date : {Dmy(d.Key.Dt)}    Unit : {d.Key.Unit}" +
                                   (Str(f, "SALE_CENTRE") != "" ? $"    Sale Centre : {Str(f, "SALE_CENTRE")}" : ""));
                    var to = string.Join(", ", new[] { Str(f, "PARTY"), Str(f, "P_ADD1"), Str(f, "P_ADD2"), Str(f, "P_STATE") }.Where(x => x != ""));
                    if (to != "") s.AddGroupLine("To : " + to + (Str(f, "P_GST_NO") != "" ? "    " + Str(f, "P_GST_NO") : ""));
                    var by = new List<string>();
                    if (Str(f, "BROKER") != "") by.Add("Broker : " + Str(f, "BROKER"));
                    var wh = string.Join(", ", new[] { Str(f, "WAREHOUSE"), Str(f, "ADD1"), Str(f, "ADD2") }.Where(x => x != ""));
                    if (wh != "") by.Add("Warehouse : " + wh);
                    if (by.Count > 0) s.AddGroupLine(string.Join("    ", by));
                    foreach (var r in d.OrderBy(r => Dec(r, "SRLNO")))
                        s.Add(Dec(r, "SRLNO"), Str(r, "GARDEN"), Str(r, "MARK"), Str(r, "INVNO"), Str(r, "GRADE"), Str(r, "LOTNO"), Str(r, "SALE_NO"),
                              Dt(r, "SALE_DATE"), Dt(r, "PROMPT_DATE"), Dec(r, "CHEST_NO") == 0 ? "" : Dec(r, "CHEST_NO").ToString("0"), Dec(r, "DRAW_QTY"));
                    s.AddSubtotal(Total("Total", d));
                    s.AddBlank();
                }
                return ReportOutputResult.Send(this, doc, outputType, "Sample Gate Pass");
            }
            catch (InvalidOperationException ex) { return Message(ex.Message); }
        }
    }
}
