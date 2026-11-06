using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using TeaSaleManagement.Models.Report;

namespace TeaSaleManagement.Controllers
{
    /// <summary>
    /// MIS > Souda Book Register (04011000F) -- old HO Sales Con_HO_04011000F ("Destination Wise Detail Of Sales",
    /// Crystal SoudaBook.rpt). Rows: API HoSoudaBook/Register. Layout: Garden > Destination > Sale Centre with Qty /
    /// Value totals (as the .rpt); Excel Checklist = every column, one line per lot.
    /// </summary>
    public class SoudaBookRegisterController : HoReportControllerBase
    {
        protected override string Screen => "SoudaBookRegister";
        protected override string Api => "/api/HoSoudaBook/";
        protected override string Title => "Souda Book Register";
        protected override string[] Lookups => new[] { "GetSaleTypes", "GetGardens", "GetDestinations" };

        [HttpPost]
        public async Task<ActionResult> Report(string fromDate, string toDate, string saleTypes = "All", string gardens = "All",
            string destinations = "All", string option = "A", string crop = "A", string outputType = "Graphics")
        {
            if (!CanView) return Denied();
            try
            {
                if (!TryDate(fromDate, out var from) || !TryDate(toDate, out var to)) return Message("Enter the From / To Date (dd/mm/yyyy).");
                var data = await PostApi("Register", new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    SaleTypes = Codes(saleTypes),
                    Gardens = Codes(gardens),
                    Destinations = Codes(destinations),
                    Option = option,
                    Crop = crop
                });
                var rows = Rows(data).ToList();
                bool checklist = ReportRenderer.ParseOutput(outputType) == ReportOutput.ExcelChecklist;
                var doc = ReportDocument.Create(Title, (string)data["title"], landscape: true);
                doc.HeaderLines.Add((string)data["acYear"]);
                if ((bool?)data["truncated"] == true) doc.Notes.Add("Only the first rows are shown; narrow the selection.");

                if (checklist)
                {
                    var s = doc.AddSection(null,
                        ReportColumn.Text("Garden"), ReportColumn.Text("Garden Name"), ReportColumn.Text("Mark"), ReportColumn.Text("Destination"),
                        ReportColumn.Text("Sale Centre"), ReportColumn.Text("Invoice No"), ReportColumn.Text("Grade"), ReportColumn.Text("Grade Name"),
                        ReportColumn.Text("Category"), ReportColumn.Number("Bag/Chest", 0), ReportColumn.Number("Quantity (kgs.)", 2),
                        ReportColumn.Number("Price", 2), ReportColumn.Number("Value", 2), ReportColumn.Text("Sale No."), ReportColumn.Date("Sale Date"),
                        ReportColumn.Text("C.N.No"), ReportColumn.Date("C.N.Date"), ReportColumn.Text("Buyer's Name"), ReportColumn.Text("Broker's Name"),
                        ReportColumn.Text("D.O.No"), ReportColumn.Date("Date"), ReportColumn.Text("DI No"));
                    s.Sheet = "Sales";
                    foreach (var r in rows.OrderBy(r => Str(r, "GARDEN")))
                        s.Add(Str(r, "GLOCA"), Str(r, "GARDEN"), Str(r, "MARK"), Str(r, "DESTINATION"), Str(r, "SALESCENTRE"), Str(r, "INVNO"),
                              Str(r, "GRADE"), Str(r, "GRADENAME"), Str(r, "CATEGORY"), Dec(r, "BAG_CHEST"), Dec(r, "QTY"), Dec(r, "RATE"), Dec(r, "AMT"),
                              Str(r, "SALE_NO"), Dt(r, "SALE_DATE"), Str(r, "CONS_NO"), Dt(r, "CONS_DT"), Str(r, "BUYER"), Str(r, "BROKER"),
                              Str(r, "DONO"), Str(r, "DONO") == "" ? null : Dt(r, "DODATE"), Str(r, "DINO"));
                    return ReportOutputResult.Send(this, doc, outputType, "Souda Book Register");
                }

                var sec = doc.AddSection(null,
                    ReportColumn.Text("Invoice No", 1.1), ReportColumn.Text("Grade", 1.1), ReportColumn.Number("Bags", 0, 0.6),
                    ReportColumn.Number("Qty (Kgs)", 2, 0.9), ReportColumn.Number("Rate", 2, 0.8), ReportColumn.Number("Value", 2, 1.1),
                    ReportColumn.Text("Sale No", 0.8), ReportColumn.Date("Sale Date", 0.9), ReportColumn.Text("C.N.No", 1),
                    ReportColumn.Date("C.N.Date", 0.9), ReportColumn.Text("Buyer", 2.2), ReportColumn.Text("D.O.No", 0.9), ReportColumn.Date("D.O.Date", 0.9));
                decimal gQty = 0, gAmt = 0, gBag = 0;
                foreach (var garden in rows.GroupBy(r => Str(r, "GARDEN")).OrderBy(g => g.Key))
                {
                    sec.AddGroupLine("Garden : " + garden.Key);
                    foreach (var dest in garden.GroupBy(r => Str(r, "DESTINATION")).OrderBy(g => g.Key))
                    {
                        sec.AddGroupLine("Destination : " + dest.Key);
                        foreach (var centre in dest.GroupBy(r => Str(r, "SALESCENTRE")).OrderBy(g => g.Key))
                        {
                            sec.AddGroupLine("Sale Centre : " + centre.Key);
                            foreach (var r in centre)
                                sec.Add(Str(r, "INVNO"), Str(r, "GRADENAME"), Dec(r, "BAG_CHEST"), Dec(r, "QTY"), Dec(r, "RATE"), Dec(r, "AMT"),
                                        Str(r, "SALE_NO"), Dt(r, "SALE_DATE"), Str(r, "CONS_NO"), Dt(r, "CONS_DT"), Str(r, "BUYER"),
                                        Str(r, "DONO"), Str(r, "DONO") == "" ? null : Dt(r, "DODATE"));
                            sec.AddSubtotal("Sale Centre Total", "", centre.Sum(r => Dec(r, "BAG_CHEST")), centre.Sum(r => Dec(r, "QTY")), null,
                                            centre.Sum(r => Dec(r, "AMT")), "", null, "", null, "", "", null);
                        }
                        sec.AddSubtotal("Destination Total", "", dest.Sum(r => Dec(r, "BAG_CHEST")), dest.Sum(r => Dec(r, "QTY")), null,
                                        dest.Sum(r => Dec(r, "AMT")), "", null, "", null, "", "", null);
                    }
                    var gb = garden.Sum(r => Dec(r, "BAG_CHEST")); var gq = garden.Sum(r => Dec(r, "QTY")); var ga = garden.Sum(r => Dec(r, "AMT"));
                    sec.AddSubtotal("Garden Total", "", gb, gq, null, ga, "", null, "", null, "", "", null);
                    gBag += gb; gQty += gq; gAmt += ga;
                }
                if (rows.Count > 0) sec.AddTotal("Grand Total", "", gBag, gQty, null, gAmt, "", null, "", null, "", "", null);
                return ReportOutputResult.Send(this, doc, outputType, "Souda Book Register");
            }
            catch (InvalidOperationException ex) { return Message(ex.Message); }
        }
    }
}
