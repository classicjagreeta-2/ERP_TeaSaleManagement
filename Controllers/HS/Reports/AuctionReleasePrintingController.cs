using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using TeaSaleManagement.Models.Report;

namespace TeaSaleManagement.Controllers
{
    /// <summary>
    /// Reports > Sales > Auction Release Printing (03021300T) -- old HO Sales AuctionReleaseController (its form was
    /// titled "Cash Collection"; Crystal AuctionReleasePrinting.rpt was not supplied). Rows: API
    /// HoAuctionReleasePrint/Print. One block per release document (Doc No / Date, Sale No, Sale Centre, Broker) with
    /// its lots and totals.
    /// </summary>
    public class AuctionReleasePrintingController : HoReportControllerBase
    {
        protected override string Screen => "AuctionReleasePrinting";
        protected override string Api => "/api/HoAuctionReleasePrint/";
        protected override string Title => "Auction Release";
        protected override string[] Lookups => new[] { "GetDocuments" };

        [HttpPost]
        public async Task<ActionResult> Report(string fromDate, string toDate, string documents = "All", string outputType = "Graphics")
        {
            if (!CanView) return Denied();
            try
            {
                if (!TryDate(fromDate, out var from) || !TryDate(toDate, out var to)) return Message("Enter the From / To Date (dd/mm/yyyy).");
                var data = await PostApi("Print", new { FromDate = fromDate, ToDate = toDate, Documents = Codes(documents) });
                var rows = Rows(data).ToList();
                var doc = ReportDocument.Create("Auction Release", $"From {from:dd/MM/yyyy} To {to:dd/MM/yyyy}", landscape: true);
                var s = doc.AddSection(null,
                    ReportColumn.Number("Sl", 0, 0.3), ReportColumn.Text("Inv No", 0.8), ReportColumn.Date("Pack Date", 0.7), ReportColumn.Text("Mark", 1.4),
                    ReportColumn.Text("Grade", 0.9), ReportColumn.Text("Category", 0.8), ReportColumn.Text("AWR No", 0.9), ReportColumn.Date("AWR Date", 0.7),
                    ReportColumn.Date("Arrival", 0.7), ReportColumn.Number("AWR Qty", 0, 0.5), ReportColumn.Number("Order Qty", 0, 0.5),
                    ReportColumn.Number("Release Bags", 0, 0.6), ReportColumn.Number("Net Wt", 2, 0.6), ReportColumn.Number("Total Net Wt", 2, 0.8),
                    ReportColumn.Number("Total Gross Wt", 2, 0.8));
                object[] Total(string label, System.Collections.Generic.IEnumerable<JToken> l) => new object[]
                {
                    null, label, null, "", "", "", "", null, null, l.Sum(r => Dec(r, "AWR_QTY")), l.Sum(r => Dec(r, "ORDER_QTY")),
                    l.Sum(r => Dec(r, "RELEASE_QTY")), null, l.Sum(r => Dec(r, "TNET_WT")), l.Sum(r => Dec(r, "TGROSS_WT"))
                };
                foreach (var d in rows.GroupBy(r => new { No = Str(r, "DOCNO"), Dt = Dt(r, "DOCDT") }))
                {
                    var f = d.First();
                    s.AddGroupLine($"Doc No : {d.Key.No}   Date : {Dmy(d.Key.Dt)}   Sale No : {Str(f, "SALE_NO")}   Sale Centre : {Str(f, "SALECENTRE")}   Broker : {Str(f, "BROKERNAME")}   Unit : {Str(f, "UNIT")}");
                    foreach (var r in d)
                        s.Add(Dec(r, "SRLNO"), Str(r, "INVNO"), Dt(r, "PACK_DATE"), Str(r, "MDESC"), Str(r, "GRADENAME"), Str(r, "CATEGORY"), Str(r, "AWR_NO"),
                              Dt(r, "AWR_DATE"), Dt(r, "ARRIVAL_DATE"), Dec(r, "AWR_QTY"), Dec(r, "ORDER_QTY"), Dec(r, "RELEASE_QTY"), Dec(r, "NET_WT"),
                              Dec(r, "TNET_WT"), Dec(r, "TGROSS_WT"));
                    s.AddSubtotal(Total("Total", d));
                }
                if (rows.Count > 0) s.AddTotal(Total("Grand Total", rows));
                return ReportOutputResult.Send(this, doc, outputType, "Auction Release");
            }
            catch (InvalidOperationException ex) { return Message(ex.Message); }
        }
    }
}
