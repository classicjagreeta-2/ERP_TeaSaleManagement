using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using TeaSaleManagement.Models;
using TeaSaleManagement.Models.Report;

namespace TeaSaleManagement.Controllers
{
    /// <summary>
    /// MIS > Unsold List (04010800F) -- old HO Sales UnsoldTeaController (Crystal UnsoldList_1 for JST, UnsoldList
    /// for other units). Rows: API HoUnsoldList/Register. Layouts:
    ///   List          = JST: Garden > Destination with destination / garden / grand totals and the crop-year
    ///                   summary (stock by manufacture financial year); others: Garden with garden / grand totals.
    ///   Excel Checklist = the old "Export To Excel" columns plus the leaf-type summary.
    ///   Stock Ageing  = the old "Ageing Excel" (JST): kgs per Garden x Location by age 90 / 180 / 365 / 730 days
    ///                   (age from the arrival date, else the despatch date) and the detail list.
    /// </summary>
    public class UnsoldListController : HoReportControllerBase
    {
        protected override string Screen => "UnsoldList";
        protected override string Api => "/api/HoUnsoldList/";
        protected override string Title => "Unsold List";
        protected override string[] Lookups => new[] { "GetOptions", "GetGardens", "GetMarks", "GetDestinations", "GetCategories", "GetGrades" };

        public async Task<ActionResult> Options(string unit)
        {
            if (!CanView) return Json(new { jst = false }, JsonRequestBehavior.AllowGet);
            var r = await Services.GetAsync<JObject>(Api + "GetOptions?unit=" + E(unit));
            return Json(new { jst = r.IsSuccessStatusCode && (bool?)r.Data?["jst"] == true }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<ActionResult> Report(string asOnDate, string unit, string gardens = "All", string marks = "All", string destinations = "All",
            string categories = "All", string grades = "All", string basedOn = "D", string crop = "A", bool salesInvoiceOnly = false,
            bool negativeStock = false, bool includeAuctionRelease = false, string layout = "L", string outputType = "Graphics")
        {
            if (!CanView) return Denied();
            try
            {
                if (!TryDate(asOnDate, out var asOn)) return Message("Enter the As on Date (dd/mm/yyyy).");
                if (string.IsNullOrWhiteSpace(unit)) return Message("Select a Unit.");
                var data = await PostApi("Register", new
                {
                    AsOnDate = asOnDate,
                    Unit = unit,
                    Gardens = Codes(gardens),
                    Marks = Codes(marks),
                    Destinations = Codes(destinations),
                    Categories = Codes(categories),
                    Grades = Codes(grades),
                    BasedOn = basedOn,
                    Crop = crop,
                    SalesInvoiceOnly = salesInvoiceOnly,
                    NegativeStock = negativeStock,
                    IncludeAuctionRelease = includeAuctionRelease
                });
                var rows = Rows(data).ToList();
                bool jst = (bool?)data["jst"] == true;
                var title = (string)data["title"];
                ReportDocument doc;
                if ((layout ?? "").Trim().ToUpperInvariant() == "A") doc = Ageing(rows, jst, asOn);
                else if (ReportRenderer.ParseOutput(outputType) == ReportOutput.ExcelChecklist) doc = Export(rows, jst, title, "UnsoldList", true);
                else doc = jst ? ListJst(rows, title) : ListOther(rows, title);
                return ReportOutputResult.Send(this, doc, outputType, layout == "A" ? "Stock Ageing" : "Unsold List");
            }
            catch (InvalidOperationException ex) { return Message(ex.Message); }
        }

        private ReportDocument ListJst(List<JToken> rows, string title)
        {
            var doc = ReportDocument.Create(Title, title);
            var s = doc.AddSection(null,
                ReportColumn.Text("Inv No", 0.8), ReportColumn.Text("Mark", 1.3), ReportColumn.Date("MFG", 0.7), ReportColumn.Date("Pack Date", 0.7),
                ReportColumn.Text("Grade", 0.8), ReportColumn.Number("Pkg", 0, 0.5), ReportColumn.Number("Nett Kgs", 2, 0.8), ReportColumn.Date("Desp Date", 0.7),
                ReportColumn.Text("CN No", 0.8), ReportColumn.Date("CN Date", 0.7), ReportColumn.Text("WH", 0.9), ReportColumn.Date("Arr Date", 0.7),
                ReportColumn.Text("Remarks", 1.2));
            object[] Total(string label, IEnumerable<JToken> l) =>
                new object[] { label, "", null, null, "", l.Sum(r => Dec(r, "BAG")), l.Sum(r => Dec(r, "NET_WT")), null, "", null, "", null, "" };
            foreach (var garden in rows.GroupBy(r => Str(r, "GARDEN")).OrderBy(g => g.Key))
            {
                s.AddGroupLine(garden.Key);
                foreach (var dest in garden.GroupBy(r => Str(r, "DEST_TO")).OrderBy(g => g.Key))
                {
                    s.AddGroupLine("    " + dest.Key);
                    foreach (var r in dest.OrderBy(r => Str(r, "MARK")).ThenBy(r => Str(r, "SEASON")).ThenBy(r => Str(r, "GINVNO")))
                        s.Add(Str(r, "INVNO"), Str(r, "MDESC"), Dt(r, "MFG_TO"), Dt(r, "DATE_ORA"), Str(r, "GRADENAME"), Dec(r, "BAG"), Dec(r, "NET_WT"),
                              Dt(r, "DESP_DATE"), Str(r, "CNNO"), Dt(r, "CNDT"), Str(r, "WAREHOUSE"), Dt(r, "ARRIVAL_DATE"), Str(r, "SALENO"));
                    s.AddSubtotal(Total(dest.Key + " - Total", dest));
                }
                s.AddSubtotal(Total(garden.Key + " - Total", garden));
            }
            if (rows.Count > 0) s.AddTotal(Total("Grand Total", rows));

            // Crop-year summary: stock by the financial year (April - March) of the manufacture date.
            var byYear = rows.Where(r => Dt(r, "MFG_TO").HasValue)
                             .GroupBy(r => { var d = Dt(r, "MFG_TO").Value; return d.Month >= 4 ? d.Year : d.Year - 1; })
                             .OrderBy(g => g.Key).ToList();
            if (byYear.Count > 0)
            {
                var y = doc.AddSection("Crop Year Summary", ReportColumn.Text("Crop Year", 2), ReportColumn.Number("Pkg", 0, 1), ReportColumn.Number("Nett Kgs", 2, 1));
                foreach (var g in byYear)
                    y.Add($"APR-{g.Key} MAR-{g.Key + 1}", g.Sum(r => Dec(r, "BAG")), g.Sum(r => Dec(r, "NET_WT")));
            }
            return doc;
        }

        private ReportDocument ListOther(List<JToken> rows, string title)
        {
            var doc = ReportDocument.Create(Title, title);
            var s = doc.AddSection(null,
                ReportColumn.Text("Mark", 1.2), ReportColumn.Text("Inv No", 0.7), ReportColumn.Text("Crop Yr.", 0.5), ReportColumn.Date("Pack Date", 0.7),
                ReportColumn.Text("Grade", 0.9), ReportColumn.Number("Pkg", 0, 0.5), ReportColumn.Number("Nett Kgs", 2, 0.8), ReportColumn.Text("Dest.", 0.8),
                ReportColumn.Date("Desp Date", 0.7), ReportColumn.Text("WH", 0.9), ReportColumn.Date("Arr Date", 0.7), ReportColumn.Text("Remarks", 1.2));
            object[] Total(string label, IEnumerable<JToken> l) =>
                new object[] { label, "", "", null, "", l.Sum(r => Dec(r, "BAG")), l.Sum(r => Dec(r, "NET_WT")), "", null, "", null, "" };
            foreach (var garden in rows.GroupBy(r => Str(r, "GARDEN")).OrderBy(g => g.Key))
            {
                s.AddGroupLine(garden.Key);
                foreach (var r in garden.OrderBy(r => Str(r, "SEASON")).ThenBy(r => Str(r, "GINVNO")))
                    s.Add(Str(r, "MDESC"), Str(r, "INVNO"), Str(r, "SEASON"), Dt(r, "DATE_ORA"), Str(r, "GRADENAME"), Dec(r, "BAG"), Dec(r, "NET_WT"),
                          Str(r, "DEST_TO"), Dt(r, "DESP_DATE"), Str(r, "WAREHOUSE"), Dt(r, "ARRIVAL_DATE"), Str(r, "SALENO"));
                s.AddSubtotal(Total("Garden Total", garden));
            }
            if (rows.Count > 0) s.AddTotal(Total("Grand Total", rows));
            return doc;
        }

        /// <summary>The old "Export To Excel" columns (withPackDate) or the ageing detail (without), plus the leaf-type summary.</summary>
        private ReportDocument Export(List<JToken> rows, bool jst, string title, string sheet, bool withPackDate, ReportDocument doc = null)
        {
            doc = doc ?? ReportDocument.Create(Title, title, landscape: true);
            var cols = new List<ReportColumn> { ReportColumn.Text("Garden"), ReportColumn.Text("Mark"), ReportColumn.Text("Inv No") };
            if (withPackDate) cols.Add(ReportColumn.Date("Pack Date"));
            cols.AddRange(new[]
            {
                ReportColumn.Text("Crop Yr."), ReportColumn.Text("Grade"), ReportColumn.Text("Category"), ReportColumn.Number("Pkg", 0),
                ReportColumn.Number("Nett Kgs", 2), ReportColumn.Number("Shortage Kgs", 2), ReportColumn.Text("Desti"), ReportColumn.Date("Desp Date"),
                ReportColumn.Text("CN NO"), ReportColumn.Date("CN DT"), ReportColumn.Text("WH"), ReportColumn.Date("Arr Date"), ReportColumn.Text("Location"),
                ReportColumn.Text("Offer"), ReportColumn.Text("Sale No"), ReportColumn.Text("Garden Code"), ReportColumn.Text("Grade Code"),
                ReportColumn.Text("MFG Month"), ReportColumn.Text("Conf No"), ReportColumn.Date("Conf Date"), ReportColumn.Number("Sale Rate", 2),
                ReportColumn.Text("Sale Centre"), ReportColumn.Text("BUYER"), ReportColumn.Number("Lying Days", 0)
            });
            if (jst)
                cols.AddRange(new[] { ReportColumn.Text("Sauda No"), ReportColumn.Date("Sauda Date"), ReportColumn.Text("Sauda Broker"),
                                      ReportColumn.Text("Sauda Party"), ReportColumn.Number("Sauda Rate", 2) });
            var s = doc.AddSection(null, cols.ToArray());
            s.Sheet = sheet;
            foreach (var r in rows)
            {
                var v = new List<object> { Str(r, "GARDEN"), Str(r, "MDESC"), Str(r, "INVNO") };
                if (withPackDate) v.Add(Dt(r, "DATE_ORA"));
                var mfg = Dt(r, "MFG_TO");
                v.AddRange(new object[]
                {
                    Str(r, "SEASON"), Str(r, "GRADENAME"), Str(r, "CATEGORY"), Dec(r, "BAG"), Dec(r, "NET_WT"), Dec(r, "SR_KGS"), Str(r, "DEST_TO"),
                    Dt(r, "DESP_DATE"), Str(r, "CNNO"), Dt(r, "CNDT"), Str(r, "WAREHOUSE"), Dt(r, "ARRIVAL_DATE"), Str(r, "LOCATION"), Str(r, "OFFERNO"),
                    Str(r, "SALENO"), Str(r, "CODE"), Str(r, "GRADE"), mfg.HasValue ? mfg.Value.ToString("MMM-yy") : "", Str(r, "CONF_NO"), Dt(r, "CONF_DT"),
                    DecN(r, "RATE"), Str(r, "SALE_CENTRE"), Str(r, "BUYER"), DecN(r, "DAYS")
                });
                if (jst) v.AddRange(new object[] { Str(r, "SAUDANO"), Dt(r, "SAUDADT"), Str(r, "SAUDA_BROKER"), Str(r, "SAUDA_PARTY"), DecN(r, "SAUDARATE") });
                s.Add(v.ToArray());
            }
            var leaf = doc.AddSection("Leaf Type Summary", ReportColumn.Text("Code"), ReportColumn.Text("Leaf Type"), ReportColumn.Number("Pkg", 0), ReportColumn.Number("Quantity", 2));
            foreach (var g in rows.GroupBy(r => new { C = Str(r, "LEAF_TYPE"), N = Str(r, "LEAF_TYPE_NAME") }).OrderBy(g => g.Key.C))
                leaf.Add(g.Key.C, g.Key.N, g.Sum(r => Dec(r, "BAG_CHEST")), g.Sum(r => Dec(r, "NET_WT")));
            if (rows.Count > 0) leaf.AddTotal("Total", "", rows.Sum(r => Dec(r, "BAG_CHEST")), rows.Sum(r => Dec(r, "NET_WT")));
            return doc;
        }

        private ReportDocument Ageing(List<JToken> rows, bool jst, DateTime asOn)
        {
            var doc = ReportDocument.Create("Stock Ageing", $"Stock Ageing Summary as on {asOn:dd/MM/yyyy}", landscape: true);
            var s = doc.AddSection(null,
                ReportColumn.Text("Garden / Unit", 1.6), ReportColumn.Text("Location", 1.2), ReportColumn.Number("Total kgs", 2, 1),
                ReportColumn.Number("KG up to 90 days", 2, 1), ReportColumn.Number("KG up to 180 days", 2, 1), ReportColumn.Number("KG up to 365 days", 2, 1),
                ReportColumn.Number("KG up to 730 days", 2, 1), ReportColumn.Number("KG above 730 days", 2, 1));
            s.Sheet = "Stock Ageing Summary";
            decimal Bucket(IEnumerable<JToken> l, int fromDays, int toDays) => l.Sum(r =>
            {
                var d = Dt(r, "ARRIVAL_DATE") ?? Dt(r, "DESP_DATE");
                if (!d.HasValue) return 0m;
                var age = (asOn.Date - d.Value.Date).TotalDays;
                return age >= fromDays && age < toDays ? Dec(r, "NET_WT") : 0m;
            });
            object[] Line(string a, string b, IEnumerable<JToken> l) => new object[]
            {
                a, b, l.Sum(r => Dec(r, "NET_WT")), Bucket(l, 0, 90), Bucket(l, 90, 180), Bucket(l, 180, 365), Bucket(l, 365, 730), Bucket(l, 730, int.MaxValue)
            };
            foreach (var g in rows.GroupBy(r => new { G = Str(r, "GARDEN"), D = Str(r, "DEST_TO") }).OrderBy(g => g.Key.G).ThenBy(g => g.Key.D))
                s.Add(Line(g.Key.G, g.Key.D, g));
            if (rows.Count > 0) s.AddTotal(Line("Total", "", rows));

            var before = doc.Sections.Count;
            Export(rows, jst, "", "Stock Ageing Detail", false, doc);
            doc.Sections[before].Title = $"Stock Ageing Detail as on {asOn:dd/MM/yyyy}";
            return doc;
        }
    }
}
