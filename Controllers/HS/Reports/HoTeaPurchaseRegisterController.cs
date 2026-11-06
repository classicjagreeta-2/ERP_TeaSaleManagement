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
    /// MIS > Tea Purchase Register (04011600F) -- old HO Sales Con_HO_04011600F (Crystal TeaPurchase /
    /// TeaPurchasePartyWise / TeaPurchasePartyWiseSummary). Rows: API HoTeaPurchaseRegister/Register (signed lines,
    /// round off once per document). Details = Purchase Location > Doc / Bill with bill, location and grand totals;
    /// Party Wise = one line per bill under each party; Summary = one line per party. Excel Checklist = the old
    /// "Excel" sheet's columns, one line per purchase line.
    /// Controller name "HoTeaPurchaseRegister": "TeaPurchaseRegister" is the PT module's own screen.
    /// </summary>
    public class HoTeaPurchaseRegisterController : HoReportControllerBase
    {
        protected override string Screen => "HoTeaPurchaseRegister";
        protected override string Api => "/api/HoTeaPurchaseRegister/";
        protected override string Title => "Tea Purchase Register";
        protected override string[] Lookups => new[] { "GetPurchaseTypes", "GetPurchaseLocations", "GetDocuments" };

        private sealed class Line
        {
            public string Centre, DocNo, BillNo, PCode, Party, InvNo, Grade, Garden, Allocation, Organic;
            public DateTime? DocDt, BillDt;
            public decimal Bag, Qty, Rate, Amount, Gross, Sgst, Cgst, Igst, Tcs, Misc, Round, Tot;
        }

        [HttpPost]
        public async Task<ActionResult> Report(string fromDate, string toDate, string purType, string unit = "", string documents = "All",
            string locations = "All", string option = "D", string garden = "both", string outputType = "Graphics")
        {
            if (!CanView) return Denied();
            try
            {
                if (!TryDate(fromDate, out var from) || !TryDate(toDate, out var to)) return Message("Enter the From / To Date (dd/mm/yyyy).");
                if (string.IsNullOrWhiteSpace(purType)) return Message("Select the Purchase Type.");
                var data = await PostApi("Register", new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    PurType = purType,
                    Unit = unit,
                    Documents = Codes(documents),
                    Locations = Codes(locations),
                    Garden = garden
                });
                var lines = Rows(data).Select(r => new Line
                {
                    Centre = Str(r, "SALECENTRE"), DocNo = Str(r, "DOCNO"), DocDt = Dt(r, "DOCDT"), BillNo = Str(r, "BILLNO"), BillDt = Dt(r, "BILLDT"),
                    PCode = Str(r, "PCODE"), Party = Str(r, "PARTY"), InvNo = Str(r, "INVNO"), Grade = Str(r, "GRADE_NAME"), Garden = Str(r, "GARDEN"),
                    Allocation = Str(r, "ALLOCATION"), Organic = Str(r, "ORGANIC"),
                    Bag = Dec(r, "BAG_CHEST"), Qty = Dec(r, "QTY"), Rate = Dec(r, "RATE"), Amount = Dec(r, "AMOUNT"), Gross = Dec(r, "GROSS"),
                    Sgst = Dec(r, "SGST_AMT"), Cgst = Dec(r, "CGST_AMT"), Igst = Dec(r, "IGST_AMT"), Tcs = Dec(r, "TCS_AMT"), Misc = Dec(r, "MISC_CHGS"),
                    Round = Dec(r, "ROUNDOFF"), Tot = Dec(r, "TOT_AMT")
                }).ToList();

                var typeName = (string)data["typeName"];
                var gardenCaption = (string)data["gardenCaption"];
                var period = $"For the Period {from:dd/MMM/yyyy} To {to:dd/MMM/yyyy}";
                option = (option ?? "D").Trim().ToUpperInvariant();
                bool checklist = ReportRenderer.ParseOutput(outputType) == ReportOutput.ExcelChecklist;

                ReportDocument doc;
                if (checklist) doc = Checklist(lines, typeName, gardenCaption, period);
                else if (option == "P") doc = PartyWise(lines, typeName, gardenCaption, period);
                else if (option == "S") doc = Summary(lines, typeName, gardenCaption, period);
                else doc = Details(lines, typeName, gardenCaption, period);
                doc.HeaderLines.Add((string)data["acYear"] + "    Unit : " + (string)data["unit"]);
                return ReportOutputResult.Send(this, doc, outputType, "Tea Purchase Register");
            }
            catch (InvalidOperationException ex) { return Message(ex.Message); }
        }

        private static object[] Amounts(IEnumerable<Line> l, bool withRound) => new object[]
        {
            l.Sum(x => x.Bag), l.Sum(x => x.Qty), null, l.Sum(x => x.Amount), l.Sum(x => x.Sgst), l.Sum(x => x.Cgst), l.Sum(x => x.Igst),
            l.Sum(x => x.Tcs), l.Sum(x => x.Misc), withRound ? l.Sum(x => x.Round) : (decimal?)null, l.Sum(x => x.Tot) + l.Sum(x => x.Round)
        };

        private ReportDocument Details(List<Line> lines, string typeName, string gardenCaption, string period)
        {
            var doc = ReportDocument.Create(Title, $"{typeName} - Tea Purchase Register {gardenCaption} {period}", landscape: true);
            var s = doc.AddSection(null,
                ReportColumn.Text("Inv No", 0.9), ReportColumn.Text("Grade", 1), ReportColumn.Text("Garden", 1.6),
                ReportColumn.Number("Bags", 0, 0.6), ReportColumn.Number("Kgs", 2, 0.9), ReportColumn.Number("Rate", 2, 0.8),
                ReportColumn.Number("Amount", 2, 1.1), ReportColumn.Number("SGST", 2, 0.9), ReportColumn.Number("CGST", 2, 0.9),
                ReportColumn.Number("IGST", 2, 0.9), ReportColumn.Number("TCS", 2, 0.8), ReportColumn.Number("Misc Chg", 2, 0.8),
                ReportColumn.Number("Round Off", 2, 0.7), ReportColumn.Number("Net Amount", 2, 1.1));
            foreach (var centre in lines.GroupBy(l => l.Centre).OrderBy(g => g.Key))
            {
                s.AddGroupLine(centre.Key == "" ? "(No Purchase Location)" : centre.Key);
                foreach (var bill in centre.GroupBy(l => new { l.DocNo, l.DocDt, l.BillNo, l.BillDt }).OrderBy(g => g.Key.DocDt).ThenBy(g => g.Key.DocNo).ThenBy(g => g.Key.BillNo))
                {
                    var f = bill.First();
                    s.AddGroupLine($"Doc No {bill.Key.DocNo}  {Dmy(bill.Key.DocDt)}    Bill No {bill.Key.BillNo}  {Dmy(bill.Key.BillDt)}    {f.PCode}  {f.Party}");
                    foreach (var l in bill)
                        s.Add(l.InvNo, l.Grade, l.Garden, l.Bag, l.Qty, l.Rate, l.Amount, l.Sgst, l.Cgst, l.Igst, l.Tcs, l.Misc, null, l.Tot);
                    s.AddSubtotal(new object[] { "Total", "", "" }.Concat(Amounts(bill, true)).ToArray());
                }
                s.AddSubtotal(new object[] { $"TOTAL {centre.Key} PURCHASE", "", "" }.Concat(Amounts(centre, true)).ToArray());
            }
            if (lines.Count > 0) s.AddTotal(new object[] { "Grand Total", "", "" }.Concat(Amounts(lines, true)).ToArray());
            return doc;
        }

        private ReportDocument PartyWise(List<Line> lines, string typeName, string gardenCaption, string period)
        {
            var doc = ReportDocument.Create(Title, $"{typeName} - Tea Purchase Register Party wise Details {gardenCaption} {period}", landscape: true);
            var s = doc.AddSection(null,
                ReportColumn.Text("Bill No", 1), ReportColumn.Date("Bill Date", 0.8), ReportColumn.Number("Bags", 0, 0.6), ReportColumn.Number("Kgs", 2, 0.9),
                ReportColumn.Text("", 0.1), ReportColumn.Number("Amount", 2, 1.1), ReportColumn.Number("SGST", 2, 0.9), ReportColumn.Number("CGST", 2, 0.9),
                ReportColumn.Number("IGST", 2, 0.9), ReportColumn.Number("TCS", 2, 0.8), ReportColumn.Number("Misc Chg", 2, 0.8),
                ReportColumn.Number("Round Off", 2, 0.7), ReportColumn.Number("Net Amount", 2, 1.1), ReportColumn.Text("MR No", 0.9),
                ReportColumn.Text("Allocation", 1.2), ReportColumn.Text("Organic Type", 1));
            foreach (var party in lines.Where(l => l.Party != "" || l.PCode != "").GroupBy(l => new { l.PCode, l.Party }).OrderBy(g => g.Key.PCode))
            {
                s.AddGroupLine($"{party.Key.PCode}  {party.Key.Party}");
                foreach (var bill in party.GroupBy(l => l.BillNo))
                {
                    var f = bill.First();
                    s.Add(new object[] { bill.Key, f.BillDt }.Concat(Amounts(bill, true)).Concat(new object[] { f.DocNo, f.Allocation, f.Organic }).ToArray());
                }
                s.AddSubtotal(new object[] { "Total", null }.Concat(Amounts(party, true)).Concat(new object[] { "", "", "" }).ToArray());
            }
            if (lines.Count > 0) s.AddTotal(new object[] { "Grand Total", null }.Concat(Amounts(lines, true)).Concat(new object[] { "", "", "" }).ToArray());
            return doc;
        }

        private ReportDocument Summary(List<Line> lines, string typeName, string gardenCaption, string period)
        {
            var doc = ReportDocument.Create(Title, $"{typeName} - Tea Purchase Register Summary {gardenCaption} {period}", landscape: true);
            var s = doc.AddSection(null,
                ReportColumn.Text("Party Code", 0.8), ReportColumn.Text("Party Name", 2.4), ReportColumn.Number("Bags", 0, 0.6), ReportColumn.Number("Kgs", 2, 0.9),
                ReportColumn.Text("", 0.1), ReportColumn.Number("Amount", 2, 1.1), ReportColumn.Number("SGST", 2, 0.9), ReportColumn.Number("CGST", 2, 0.9),
                ReportColumn.Number("IGST", 2, 0.9), ReportColumn.Number("TCS", 2, 0.8), ReportColumn.Number("Misc Chg", 2, 0.8),
                ReportColumn.Number("Round Off", 2, 0.7), ReportColumn.Number("Net Amount", 2, 1.1), ReportColumn.Text("Allocation", 1.2));
            foreach (var party in lines.GroupBy(l => new { l.PCode, l.Party }))
                s.Add(new object[] { party.Key.PCode, party.Key.Party }.Concat(Amounts(party, true)).Concat(new object[] { party.First().Allocation }).ToArray());
            if (lines.Count > 0) s.AddTotal(new object[] { "Grand Total", "" }.Concat(Amounts(lines, true)).Concat(new object[] { "" }).ToArray());
            return doc;
        }

        private ReportDocument Checklist(List<Line> lines, string typeName, string gardenCaption, string period)
        {
            var doc = ReportDocument.Create(Title, $"{typeName} - Tea Purchase Register {gardenCaption} {period}", landscape: true);
            var s = doc.AddSection(null,
                ReportColumn.Text("Pur Loc"), ReportColumn.Text("Doc No."), ReportColumn.Date("Doc Date"), ReportColumn.Text("Bill No"), ReportColumn.Date("Bill Date"),
                ReportColumn.Text("Party Code"), ReportColumn.Text("Party Name"), ReportColumn.Text("Invoice No."), ReportColumn.Text("Grade"),
                ReportColumn.Number("Chest", 0), ReportColumn.Number("KG", 2), ReportColumn.Number("Amount", 2), ReportColumn.Text("Garden"),
                ReportColumn.Number("Gross Weight", 2), ReportColumn.Number("SGST", 2), ReportColumn.Number("CGST", 2), ReportColumn.Number("IGST", 2),
                ReportColumn.Number("TCS", 2), ReportColumn.Number("MISC CHG", 2), ReportColumn.Number("ROUND OFF", 2), ReportColumn.Number("Net Amount", 2),
                ReportColumn.Text("Allocation"), ReportColumn.Text("Organic Type"));
            s.Sheet = "Tea Purchase";
            foreach (var l in lines)
                s.Add(l.Centre, l.DocNo, l.DocDt, l.BillNo, l.BillDt, l.PCode, l.Party, l.InvNo, l.Grade, l.Bag, l.Qty, l.Amount, l.Garden, l.Gross,
                      l.Sgst, l.Cgst, l.Igst, l.Tcs, l.Misc, l.Round, l.Tot + l.Round, l.Allocation, l.Organic);
            return doc;
        }
    }
}
