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
    /// MIS > Tax Invoice Register (04010500F) -- old HO Sales HOSalesRegisterController ("SALES REGISTER").
    /// Rows: API HoSalesRegister/Register. Layouts:
    ///   Details  = Sale Centre > Garden, a total under every tax invoice of more than one line, garden / centre /
    ///              grand totals (the old "Excel Formatted", now also the PDF; its last-group total is no longer lost);
    ///              Excel Checklist = the old 47-column checklist, one line per invoice line.
    ///   Summary  = one line per tax invoice under Sale Centre > Garden > Party (the old "Excel Party Wise").
    ///   HUL      = the old HUL upload sheet (JST login only).
    /// </summary>
    public class TaxInvoiceRegisterController : HoReportControllerBase
    {
        protected override string Screen => "TaxInvoiceRegister";
        protected override string Api => "/api/HoSalesRegister/";
        protected override string Title => "Tax Invoice Register";
        protected override string[] Lookups => new[] { "GetSaleTypes", "GetGardens" };

        [HttpPost]
        public async Task<ActionResult> Report(string fromDate, string toDate, string units = "All", string gardens = "All", string saleTypes = "All",
            string option = "D", string crop = "A", bool combinedPrivate = true, string leaf = "B", string outputType = "Graphics")
        {
            if (!CanView) return Denied();
            try
            {
                if (!TryDate(fromDate, out var from) || !TryDate(toDate, out var to)) return Message("Enter the From / To Date (dd/mm/yyyy).");
                option = (option ?? "D").Trim().ToUpperInvariant();
                var data = await PostApi("Register", new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    Units = Codes(units),
                    Gardens = Codes(gardens),
                    SaleTypes = Codes(saleTypes),
                    Option = option,
                    Crop = crop,
                    CombinedPrivate = combinedPrivate,
                    Leaf = leaf
                });
                var rows = Rows(data).ToList();
                var types = (string)data["saleTypes"];
                var cropCaption = (string)data["cropCaption"];
                var period = $"Period From {from:dd/MM/yyyy} To {to:dd/MM/yyyy}" + (string.IsNullOrEmpty(cropCaption) ? "" : " (" + cropCaption + ")");
                bool checklist = ReportRenderer.ParseOutput(outputType) == ReportOutput.ExcelChecklist;

                ReportDocument doc;
                if (option == "H") doc = Hul(rows, period);
                else if (option == "S") doc = Summary(rows, types, period);
                else if (checklist) doc = Checklist(rows, types, period);
                else doc = Details(rows, types, period);
                doc.HeaderLines.Add((string)data["acYear"]);
                if ((bool?)data["truncated"] == true) doc.Notes.Add("Only the first rows are shown; narrow the selection.");
                return ReportOutputResult.Send(this, doc, outputType, option == "H" ? "HUL" : "Tax Invoice Register");
            }
            catch (InvalidOperationException ex) { return Message(ex.Message); }
        }

        private static decimal Rate(decimal amt, decimal qty) => qty == 0 ? 0 : Math.Round(amt / qty, 2);

        // Chest, Kgs sold / sample / other / total, rate, amount, discount, taxable, IGST, CGST, SGST, GST, TCS, total.
        private static object[] Sums(IEnumerable<JToken> l)
        {
            var list = l.ToList();
            var qty = list.Sum(r => Dec(r, "TOT_QTY"));
            var amt = list.Sum(r => Dec(r, "AMOUNT"));
            return new object[]
            {
                list.Sum(r => Dec(r, "BAG_CHEST")), list.Sum(r => Dec(r, "BILL_QTY")), list.Sum(r => Dec(r, "SAMPLE_QTY")), list.Sum(r => Dec(r, "OTHER_QTY")),
                qty, Rate(amt, qty), amt, list.Sum(r => Dec(r, "DISCOUNT")), list.Sum(r => Dec(r, "TAXABLE")), list.Sum(r => Dec(r, "IGST_AMT")),
                list.Sum(r => Dec(r, "CGST_AMT")), list.Sum(r => Dec(r, "SGST_AMT")), list.Sum(r => Dec(r, "GST_AMT")), list.Sum(r => Dec(r, "TCS_AMT")),
                list.Sum(r => Dec(r, "TOT_AMT"))
            };
        }

        private static ReportColumn[] AmountColumns() => new[]
        {
            ReportColumn.Number("Chest", 0, 0.5), ReportColumn.Number("Sold Kgs", 2, 0.8), ReportColumn.Number("Sam.Sht.", 2, 0.6),
            ReportColumn.Number("Oth.Sht.", 2, 0.6), ReportColumn.Number("Total Kgs", 2, 0.8), ReportColumn.Number("Rate", 2, 0.6),
            ReportColumn.Number("Amount", 2, 1), ReportColumn.Number("Discount", 2, 0.7), ReportColumn.Number("Taxable", 2, 1),
            ReportColumn.Number("IGST", 2, 0.8), ReportColumn.Number("CGST", 2, 0.8), ReportColumn.Number("SGST", 2, 0.8),
            ReportColumn.Number("GST", 2, 0.8), ReportColumn.Number("TCS", 2, 0.6), ReportColumn.Number("Total", 2, 1)
        };

        private ReportDocument Details(List<JToken> rows, string types, string period)
        {
            var doc = ReportDocument.Create(Title, $"TAX INVOICE REGISTER ({types}) - {period}", landscape: true);
            var cols = new List<ReportColumn>
            {
                ReportColumn.Text("Mark", 1), ReportColumn.Text("Sale No", 0.5), ReportColumn.Text("Tax Invoice No", 1), ReportColumn.Date("Date", 0.7),
                ReportColumn.Text("Party", 1.6), ReportColumn.Text("Broker", 1), ReportColumn.Text("Grade", 0.7), ReportColumn.Text("Inv No", 0.7)
            };
            cols.AddRange(AmountColumns());
            cols.Add(ReportColumn.Text("Sale Type", 1));
            var s = doc.AddSection(null, cols.ToArray());
            object[] Total(string label, IEnumerable<JToken> l) =>
                new object[] { label, "", "", null, "", "", "", "" }.Concat(Sums(l)).Concat(new object[] { "" }).ToArray();

            foreach (var centre in rows.GroupBy(r => Str(r, "SALECENTRE")))
            {
                s.AddGroupLine("Sales Centre : " + centre.Key);
                foreach (var garden in centre.GroupBy(r => Str(r, "GARDEN")))
                {
                    s.AddGroupLine("Estate : " + garden.Key);
                    foreach (var inv in garden.GroupBy(r => Str(r, "DESP_NO")))
                    {
                        bool first = true;
                        foreach (var r in inv)
                        {
                            s.Add(new object[]
                            {
                                Str(r, "MDESC"), first ? Str(r, "SALE_NO") : "", first ? inv.Key : "", first ? Dt(r, "DESP_DATE") : null,
                                first ? Str(r, "PARTY_NAME") : "", first ? Str(r, "BROK_NAME") : "", Str(r, "GRADENAME"), Str(r, "INVNO")
                            }.Concat(Sums(new[] { r })).Concat(new object[] { Str(r, "SALE_TYPE_NAME") }).ToArray());
                            first = false;
                        }
                        if (inv.Count() > 1) s.AddSubtotal(Total("Invoice Total", inv));
                    }
                    s.AddSubtotal(Total("Estate Total", garden));
                }
                s.AddSubtotal(Total("Sales Centre Total", centre));
            }
            if (rows.Count > 0) s.AddTotal(Total("Grand Total", rows));
            return doc;
        }

        private ReportDocument Summary(List<JToken> rows, string types, string period)
        {
            var doc = ReportDocument.Create(Title, $"TAX INVOICE REGISTER (SUMMARY) - ({types}) - {period}", landscape: true);
            var cols = new List<ReportColumn>
            {
                ReportColumn.Text("Party Code", 0.6), ReportColumn.Text("Party", 1.6), ReportColumn.Text("Tax Invoice No", 1), ReportColumn.Date("Date", 0.7),
                ReportColumn.Text("Broker", 1)
            };
            cols.AddRange(AmountColumns());
            cols.Add(ReportColumn.Number("Brok. %", 2, 0.5));
            cols.Add(ReportColumn.Number("Brokerage", 2, 0.8));
            cols.Add(ReportColumn.Text("Sale Type", 1));
            var s = doc.AddSection(null, cols.ToArray());
            object[] Total(string label, IEnumerable<JToken> l) =>
                new object[] { label, "", "", null, "" }.Concat(Sums(l)).Concat(new object[] { null, l.Sum(r => Dec(r, "BROKERAGE")), "" }).ToArray();

            foreach (var centre in rows.GroupBy(r => Str(r, "SALECENTRE")))
            {
                s.AddGroupLine("Sales Centre : " + centre.Key);
                foreach (var garden in centre.GroupBy(r => Str(r, "GARDEN")))
                {
                    s.AddGroupLine("Estate : " + garden.Key);
                    foreach (var party in garden.GroupBy(r => Str(r, "BILL_TO_PARTY")))
                        foreach (var inv in party.GroupBy(r => Str(r, "DESP_NO")))
                        {
                            var f = inv.First();
                            s.Add(new object[] { party.Key, Str(f, "PARTY_NAME"), inv.Key, Dt(f, "DESP_DATE"), Str(f, "BROK_NAME") }
                                .Concat(Sums(inv)).Concat(new object[] { Dec(f, "BROKERAGE_RATE"), inv.Sum(r => Dec(r, "BROKERAGE")), Str(f, "SALE_TYPE_NAME") }).ToArray());
                        }
                    s.AddSubtotal(Total("Estate Total", garden));
                }
                s.AddSubtotal(Total("Sales Centre Total", centre));
            }
            if (rows.Count > 0) s.AddTotal(Total("Grand Total", rows));
            return doc;
        }

        private ReportDocument Checklist(List<JToken> rows, string types, string period)
        {
            var doc = ReportDocument.Create(Title, $"TAX INVOICE REGISTER ({types}) - {period}", landscape: true);
            var s = doc.AddSection(null,
                ReportColumn.Text("Sales Centre"), ReportColumn.Text("GLOCA"), ReportColumn.Text("Estate Name"), ReportColumn.Text("Mark Name"),
                ReportColumn.Text("Sale No"), ReportColumn.Date("Sale Date"), ReportColumn.Text("Tax Invoice No"), ReportColumn.Date("Tax Invoice Date"),
                ReportColumn.Text("BL No"), ReportColumn.Date("BL Date"), ReportColumn.Text("Bill To Party Code"), ReportColumn.Text("Bill To Party Name"),
                ReportColumn.Text("Bill To Party GST No"), ReportColumn.Text("Ship To Party Code"), ReportColumn.Text("Ship To Party Name"),
                ReportColumn.Text("Ship To Party GST No"), ReportColumn.Text("Brok Code"), ReportColumn.Text("Brok Descn"), ReportColumn.Text("Crop Year"),
                ReportColumn.Text("Grade Code"), ReportColumn.Text("Grade Name"), ReportColumn.Text("Inv No"), ReportColumn.Date("Pack Date"),
                ReportColumn.Number("No of Chest", 0), ReportColumn.Number("Sold Kgs.", 2), ReportColumn.Number("Sam.Sht. Kgs.", 2),
                ReportColumn.Number("Oth.Sht. Kgs.", 2), ReportColumn.Number("Total Kgs.", 2), ReportColumn.Number("Rate", 2), ReportColumn.Number("Amount", 2),
                ReportColumn.Number("Discount", 2), ReportColumn.Number("Taxable Amount", 2), ReportColumn.Number("IGST Amount", 2),
                ReportColumn.Number("CGST Amount", 2), ReportColumn.Number("SGST Amount", 2), ReportColumn.Number("GST Amount", 2),
                ReportColumn.Number("TCS Amount", 2), ReportColumn.Number("Total Amount", 2), ReportColumn.Number("Brokerage Rate", 2),
                ReportColumn.Number("Brokerage", 2), ReportColumn.Text("Sale Type"), ReportColumn.Text("MFG Month"), ReportColumn.Text("REMARKS"),
                ReportColumn.Text("Conf No"), ReportColumn.Date("Conf Date"), ReportColumn.Text("Order No"), ReportColumn.Date("Order Date"));
            s.Sheet = "Sales";
            foreach (var r in rows)
            {
                var saleNo = Str(r, "SALE_NO");
                var mfg = Dt(r, "MFG_TO");
                s.Add(Str(r, "SALECENTRE"), Str(r, "GLOCA"), Str(r, "GARDEN"), Str(r, "MDESC"), saleNo, saleNo == "" ? null : Dt(r, "SALE_DATE"),
                      Str(r, "DESP_NO"), Dt(r, "DESP_DATE"), Str(r, "BLNO"), Dt(r, "BLDT"), Str(r, "BILL_TO_PARTY"), Str(r, "PARTY_NAME"), Str(r, "GST_NO"),
                      Str(r, "SHIP_TO_PARTY"), Str(r, "SHIP_TO_PARTY_NAME"), Str(r, "SHIP_TO_GST_NO"), Str(r, "BROK_CODE"), Str(r, "BROK_NAME"),
                      Str(r, "SEASON"), Str(r, "GCODE"), Str(r, "GRADENAME"), Str(r, "INVNO"), Dt(r, "PACK_DATE"), Dec(r, "BAG_CHEST"), Dec(r, "BILL_QTY"),
                      Dec(r, "SAMPLE_QTY"), Dec(r, "OTHER_QTY"), Dec(r, "TOT_QTY"), Dec(r, "RATE"), Dec(r, "AMOUNT"), Dec(r, "DISCOUNT"), Dec(r, "TAXABLE"),
                      Dec(r, "IGST_AMT"), Dec(r, "CGST_AMT"), Dec(r, "SGST_AMT"), Dec(r, "GST_AMT"), Dec(r, "TCS_AMT"), Dec(r, "TOT_AMT"),
                      Dec(r, "BROKERAGE_RATE"), Dec(r, "BROKERAGE"), Str(r, "SALE_TYPE_NAME"), mfg.HasValue ? mfg.Value.ToString("MMM-yy") : "",
                      Str(r, "REMARKS"), Str(r, "ORDER_DOCNO"), Dt(r, "ORDER_DOCDT"), Str(r, "ORDNO"), Dt(r, "ORDDT"));
            }
            return doc;
        }

        private ReportDocument Hul(List<JToken> rows, string period)
        {
            var doc = ReportDocument.Create("HUL", "HUL - " + period, landscape: true);
            var cols = new List<ReportColumn>
            {
                ReportColumn.Text("SALE YEAR"), ReportColumn.Text("BUYING CENTER"), ReportColumn.Text("SALE NO"), ReportColumn.Text("LEAF TYPE"),
                ReportColumn.Text("SUPPLIER CODE"), ReportColumn.Text("CHANNEL"), ReportColumn.Text("SUPPLIER PREINVOICE NUMBER"), ReportColumn.Text("COMPANY CODE"),
                ReportColumn.Date("SUPPLIER PREINVOICE DATE"), ReportColumn.Date("PROMPT DATE"), ReportColumn.Number("BROKERAGE RATE", 2),
                ReportColumn.Text("SALES TAX CODE"), ReportColumn.Number("SL TAX PERCENTAGE", 0), ReportColumn.Number("STAMP DUTY", 0),
                ReportColumn.Text("SUR CHARGE PERCENTAGE"), ReportColumn.Text("BROKERAGE APPLICABLE(LOT MONEY YES/NO)"),
                ReportColumn.Text("SALESTAX APPLICABLE(YES/NO)"), ReportColumn.Date("SALE DATE"), ReportColumn.Text("LOT NO"),
                ReportColumn.Number("PREINVOICE PRICE", 2), ReportColumn.Number("PREINVOICE LOT QUANTITY", 2), ReportColumn.Number("NUMBER OF PACKAGE BREAKEUPS", 0),
                ReportColumn.Text("PREINVOICE GARDEN"), ReportColumn.Text("PREINVOICE GRADEN GRADE"), ReportColumn.Text("PREINVOICE GRADEN INVOICE NUMBER"),
                ReportColumn.Text("PREINVOICE PURCHASE TYPE"), ReportColumn.Text("PREINVOICE DOCUMENT NUMBER")
            };
            for (int i = 1; i <= 5; i++)
            {
                cols.Add(ReportColumn.Number("PACKAGE WEIGHT BREAKEUP" + i, 2));
                cols.Add(ReportColumn.Number(i == 1 ? "1ST BREAKEUP NUMBER OF BAGS" : i + (i == 2 ? "ND" : i == 3 ? "RD" : "TH") + " BREAKEUP NUMBER OF BAGS", 0));
            }
            cols.Add(ReportColumn.Text("WAREHOUSE CODE"));
            var s = doc.AddSection(null, cols.ToArray());
            s.Sheet = "HUL";
            foreach (var r in rows)
            {
                var desp = Dt(r, "DESP_DATE");
                var breakups = (r["BREAKUPS"] as JArray) ?? new JArray();
                var values = new List<object>
                {
                    desp.HasValue ? desp.Value.Year.ToString() : "", Str(r, "SALECENTRE"), "", "", "JTI", "NAC", Str(r, "DESP_NO"), "C1", desp, Dt(r, "PROMPT_DATE"),
                    Dec(r, "BROKERAGE_RATE"), "ZI", 5m, 0m, "", "Y", "Y", desp, Str(r, "LOTNO"), Dec(r, "RATE"), Dec(r, "BILL_QTY"), (decimal)breakups.Count,
                    Str(r, "MDESC"), Str(r, "GRADENAME"), Str(r, "INVNO"), "I", ""
                };
                for (int i = 0; i < 5; i++)
                {
                    var b = i < breakups.Count ? breakups[i] : null;
                    values.Add(b == null ? (object)null : Dec(b, "WT"));
                    values.Add(b == null ? (object)null : Dec(b, "BAGS"));
                }
                values.Add(Str(r, "BUYER_WH_CODE"));
                s.Add(values.ToArray());
            }
            return doc;
        }
    }
}
