using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TeaSaleManagement.Models.Report.HoSales
{
    /// <summary>
    /// Tax Invoice / Challan print of the HS "Tax Invoice Printing" and "Export Invoice Printing (TT)" screens --
    /// redraws the old Crystal reports (TaxInvoiceJST, TaxInvoiceJST_HUL, TaxInvoiceROS) with iTextSharp, one page
    /// set per invoice and copy (Original for Recipient / Duplicate for Transporter / Triplicate for Supplier /
    /// Extra Copy). The invoice data comes from the API (HoTaxInvoicePrint/Print); LAYOUT picks the line table:
    /// JST, HUL (Doc No containing "SH") or STD (other locations).
    /// Not reproduced: the logos / stamp pictures embedded in the .rpt files.
    /// </summary>
    public static class TaxInvoicePdf
    {
        public static readonly string[] CopyNames = { "", "Original for Recipient", "Duplicate for Transporter", "Triplicate for Supplier", "Extra Copy" };

        private const string FssText = "   We hereby certify that the food mentioned in this Invoice are warranted to be of the nature and quality it purports to be & conforms to the Food Safety and Standards Act, 2006 and rules / regulations made there under.";
        private const string PpfText = "  The Teas in the above invoices were made from Green Leaf, which have only used PPFs as recommended in Plant Protection Code & have not used any chemicals & pesticides banned in India as per the Certificate and undertaking furnished by the growers. It is further certified that from the date of Purchasing the green leaf to date of manufacturing there was no use of any pesticides and/Or any Contamination. In the event of detection of any banned pesticides or chemicals and their residues are beyond the prescribed limit Tea Board is free to take appropriate action as per the provisions of the Tea Marketing (Control) order, 2003.";
        private const string UpiText = "  We also accept payment through Debit Card/UPI/BHIM-UPI /UPI QR CODE /BHIM-UPI QR CODE";

        private static readonly BaseFont Arial = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
        private static readonly BaseFont ArialB = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
        private static readonly BaseFont Times = BaseFont.CreateFont(BaseFont.TIMES_ROMAN, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
        private static readonly BaseFont TimesB = BaseFont.CreateFont(BaseFont.TIMES_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);

        private static Font F(BaseFont b, float size) => new Font(b, size);

        private static string S(JToken t, string k) => ((string)t?[k] ?? "").Trim();
        private static decimal D(JToken t, string k)
        {
            var v = t?[k];
            if (v == null || v.Type == JTokenType.Null) return 0m;
            try { return v.Value<decimal>(); } catch { return 0m; }
        }
        private static string Dmy(JToken t, string k)
        {
            var v = t?[k];
            if (v == null || v.Type == JTokenType.Null) return "";
            if (v.Type == JTokenType.Date) return v.Value<DateTime>().ToString("dd/MM/yyyy");
            return DateTime.TryParse((string)v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("dd/MM/yyyy") : "";
        }
        private static string N(decimal v, int dec = 2) => v.ToString("N" + dec, CultureInfo.GetCultureInfo("en-IN"));
        private static string Nz(decimal v, int dec = 2) => v == 0 ? "" : N(v, dec);

        private static PdfPCell Cell(string text, Font font, int align = Element.ALIGN_LEFT, int border = Rectangle.NO_BORDER, int colspan = 1, float pad = 2f)
        {
            var c = new PdfPCell(new Phrase(text ?? "", font)) { HorizontalAlignment = align, Border = border, Colspan = colspan, Padding = pad, BorderWidth = 0.5f };
            return c;
        }

        private static PdfPCell Box(PdfPTable inner, int border = Rectangle.BOX, int colspan = 1)
        {
            return new PdfPCell(inner) { Border = border, Colspan = colspan, Padding = 0, BorderWidth = 0.5f };
        }

        private static PdfPTable Table(params float[] widths)
        {
            var t = new PdfPTable(widths.Length) { WidthPercentage = 100 };
            t.SetWidths(widths);
            t.DefaultCell.Border = Rectangle.NO_BORDER;
            return t;
        }

        /// <param name="invoices">API "invoices" array.</param>
        /// <param name="copies">Copy numbers 1..4 in print order.</param>
        /// <param name="company">Company name (first heading line, "For ..." under the signature).</param>
        public static byte[] Build(JArray invoices, IList<int> copies, string company)
        {
            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 18, 18, 22, 30);
                var writer = PdfWriter.GetInstance(doc, ms);
                writer.PageEvent = new Footer();
                doc.Open();
                bool first = true;
                foreach (var inv in invoices)
                    foreach (var copy in copies)
                    {
                        if (!first) doc.NewPage();
                        first = false;
                        Invoice(doc, inv, copy, company);
                    }
                doc.Close();
                return ms.ToArray();
            }
        }

        private static void Invoice(Document doc, JToken inv, int copy, string company)
        {
            var layout = S(inv, "LAYOUT");
            bool std = layout == "STD", hul = layout == "HUL";
            var head = S(inv, "INV_HEAD");
            var lines = (inv["lines"] as JArray) ?? new JArray();

            // ---- seller block + copy caption + QR -------------------------------------------------
            var top = Table(7800, 3400);
            var left = Table(1);
            left.AddCell(Cell(company, F(ArialB, 13)));
            var addr = S(inv, "ADDR").Replace("||", "\n");
            var phone = "Phone No : " + S(inv, "PHONE") + " Email Id : " + S(inv, "EMAIL");
            var tomco = S(inv, "TOMCO_NO") != "" ? "TMCO No. : " + S(inv, "TOMCO_NO") : "";
            left.AddCell(Cell(string.Join("\n", new[] { addr, phone, "GSTIN : " + S(inv, "GSTIN"), tomco }.Where(x => x.Trim() != "")), F(Arial, 9)));
            var cin = "CIN NO : " + S(inv, "CIN_NO") + (S(inv, "PAN_NO") != "" ? "  PAN : " + S(inv, "PAN_NO") : "") + (S(inv, "FSSAI_NO") != "" ? "  FSSAI NO. : " + S(inv, "FSSAI_NO") : "");
            left.AddCell(Cell(cin, F(ArialB, 9)));
            // Group logos: Neptune on the left, Bando on the right of the seller block.
            var logoL = Logo("neptune.png"); var logoR = Logo("bando.png");
            if (logoL != null || logoR != null)
            {
                var framed = Table(1100, 5600, 1100);
                framed.AddCell(LogoCell(logoL));
                framed.AddCell(new PdfPCell(left) { Border = Rectangle.NO_BORDER, Padding = 0 });
                framed.AddCell(LogoCell(logoR));
                left = framed;
            }
            var right = Table(1);
            right.AddCell(Cell(copy >= 1 && copy <= 4 ? CopyNames[copy] : "", F(Arial, 9), Element.ALIGN_CENTER, Rectangle.BOTTOM_BORDER));
            var qr = S(inv, "SIGNEDQRCODE");
            if (qr != "")
            {
                try
                {
                    var img = new BarcodeQRCode(qr, 1, 1, null).GetImage();
                    img.ScaleAbsolute(95, 95);
                    right.AddCell(new PdfPCell(img) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 4 });
                }
                catch { right.AddCell(Cell("", F(Arial, 8))); }
            }
            else right.AddCell(Cell(" ", F(Arial, 8)));
            top.AddCell(Box(left, Rectangle.BOX));
            top.AddCell(Box(right, Rectangle.BOX));
            doc.Add(top);

            // ---- invoice heading ------------------------------------------------------------------
            var h = Table(1);
            h.AddCell(Cell(head == "Challan" ? "CHALLAN" : "TAX  INVOICE", F(ArialB, 12), Element.ALIGN_CENTER, Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER, 1, 4));
            if (S(inv, "IRN") != "") h.AddCell(Cell("IRN : " + S(inv, "IRN"), F(ArialB, 9), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER));
            doc.Add(h);

            var no = Table(1.4f, 2.2f, 0.6f, 1.4f, 1.3f, 1.3f);
            int b = Rectangle.NO_BORDER;
            no.AddCell(Cell(head + " No :", F(ArialB, 9), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER));
            no.AddCell(Cell(S(inv, "DESP_NO"), F(ArialB, 9)));
            no.AddCell(Cell("Date :", F(ArialB, 9)));
            no.AddCell(Cell(Dmy(inv, "DESP_DATE"), F(ArialB, 9)));
            no.AddCell(Cell("Date of Supply :", F(Times, 9)));
            no.AddCell(Cell(Dmy(inv, "ISSUE_DT"), F(Times, 9), Element.ALIGN_LEFT, Rectangle.RIGHT_BORDER));
            no.AddCell(Cell("P.O.No :", F(TimesB, 9), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER));
            no.AddCell(Cell(S(inv, "ORDNO"), F(TimesB, 9), Element.ALIGN_LEFT, Rectangle.BOTTOM_BORDER));
            no.AddCell(Cell("", F(Times, 9), Element.ALIGN_LEFT, Rectangle.BOTTOM_BORDER));
            no.AddCell(Cell(hul ? "Prompt Date : " + Dmy(inv, "PROMPT_DATE") : "", F(ArialB, 8), Element.ALIGN_LEFT, Rectangle.BOTTOM_BORDER));
            no.AddCell(Cell("P.O. Date :", F(TimesB, 9), Element.ALIGN_LEFT, Rectangle.BOTTOM_BORDER));
            no.AddCell(Cell(Dmy(inv, "ORDDT"), F(TimesB, 9), Element.ALIGN_LEFT, Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER));
            doc.Add(no);

            // ---- state / transport ------------------------------------------------------------------
            var tr = Table(3200, 4600);
            var st = Table(1.1f, 2.2f);
            st.AddCell(Cell("State :", F(ArialB, 9))); st.AddCell(Cell(S(inv, "STATE_NAME"), F(ArialB, 8)));
            st.AddCell(Cell("STATE CODE :", F(ArialB, 8))); st.AddCell(Cell(S(inv, "STATE_CODE"), F(ArialB, 8)));
            var tp = Table(1.7f, 2.8f);
            tp.AddCell(Cell("Consignment No & Dt :", F(Times, 9))); tp.AddCell(Cell(S(inv, "CONS_NO"), F(Times, 9)));
            tp.AddCell(Cell("Carrier :", F(Times, 9))); tp.AddCell(Cell(S(inv, "TRANSPORTER"), F(Times, 9)));
            tp.AddCell(Cell("Transportation Mode :", F(Times, 9))); tp.AddCell(Cell(S(inv, "DESP_BY"), F(Times, 9)));
            tp.AddCell(Cell("Vehicle No. :", F(Times, 9))); tp.AddCell(Cell(S(inv, "TNO"), F(Times, 9)));
            tr.AddCell(Box(st, Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER | Rectangle.RIGHT_BORDER));
            tr.AddCell(Box(tp, Rectangle.BOTTOM_BORDER | Rectangle.RIGHT_BORDER));
            doc.Add(tr);

            // ---- receiver / consignee -----------------------------------------------------------
            var pr = Table(1, 1);
            pr.AddCell(Box(Party(inv, "B", "Detail of Receiver / Billed To"), Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER | Rectangle.RIGHT_BORDER));
            pr.AddCell(Box(Party(inv, "P", "Detail of Consignee / Shipped to :"), Rectangle.BOTTOM_BORDER | Rectangle.RIGHT_BORDER));
            doc.Add(pr);

            var pd = Table(1);
            pd.AddCell(Cell(S(inv, "PLACE_DESP"), F(Times, 9), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER));
            doc.Add(pd);

            var it = Table(6180, 5200);
            if (!hul)
            {
                it.AddCell(Cell("Broker Name : " + S(inv, "BROKER"), F(Times, 8), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER));
                it.AddCell(Cell(std ? "" : "Prompt Date : " + Dmy(inv, "PROMPT_DATE"), F(ArialB, 8), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER));
            }
            it.AddCell(Cell("Item Description : BLACK TEA - LEAF IN BULK / DUST IN BULK", F(ArialB, 8), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER));
            it.AddCell(Cell("HSN : " + S(inv, "HSN_CODE"), F(ArialB, 8), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER));
            if (std)
            {
                var mark = lines.Count > 0 ? S(lines[0], "MARK") : "";
                it.AddCell(Cell("MARK : " + mark, F(ArialB, 8), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER));
                it.AddCell(Cell("Prompt Date : " + Dmy(inv, "PROMPT_DATE"), F(ArialB, 8), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER));
            }
            doc.Add(it);

            // ---- lines --------------------------------------------------------------------------------
            doc.Add(hul ? HulLines(lines) : std ? StdLines(lines) : JstLines(lines));

            // ---- totals / words ---------------------------------------------------------------------------
            decimal taxable = lines.Sum(l => D(l, "AMOUNT") - D(l, "DISCOUNT"));
            decimal cgst = lines.Sum(l => D(l, "CGST_AMT")), sgst = lines.Sum(l => D(l, "SGST_AMT")), igst = lines.Sum(l => D(l, "IGST_AMT"));
            decimal lineTotal = D(inv, "LINE_TOTAL"), tcs = D(inv, "TCS_AMT"), other = D(inv, "OTHER_CHG"), grand = D(inv, "GRAND_TOTAL");
            var first = lines.Count > 0 ? lines[0] : null;
            var tot = Table(6870, 4510);
            var words = Table(1);
            words.AddCell(Cell("Total Invoice Amount in words :", F(ArialB, 8)));
            words.AddCell(Cell(AmountInWords(grand), F(Times, 9)));
            var sums = Table(2.4f, 0.9f, 1.2f);
            void Sum(string label, string rate, decimal v, bool bold = false)
            {
                sums.AddCell(Cell(label, F(bold ? ArialB : Arial, 8)));
                sums.AddCell(Cell(rate, F(Arial, 7.5f), Element.ALIGN_RIGHT));
                sums.AddCell(Cell(N(v), F(bold ? ArialB : Arial, 8), Element.ALIGN_RIGHT));
            }
            Sum("Total Amount Before Tax", "", taxable, true);
            if (std)
            {
                Sum("Add : CGST", "@ " + N(D(first, "CGST_RT")) + " %", cgst);
                Sum("Add : SGST", "@ " + N(D(first, "SGST_RT")) + " %", sgst);
                Sum("Add : IGST", "@ " + N(D(first, "IGST_RT")) + " %", igst);
            }
            else
            {
                bool isIgst = D(first, "IGST_RT") > 0;
                Sum("Add : " + (isIgst ? "IGST" : "CGST"), "@ " + N(isIgst ? D(first, "IGST_RT") : D(first, "CGST_RT")) + " %", isIgst ? igst : cgst);
                Sum("Add : SGST", "@ " + N(D(first, "SGST_RT")) + " %", sgst);
            }
            Sum("Tax Amount : GST", "", cgst + sgst + igst, true);
            if (tcs != 0) Sum("Add : TCS @ " + N(D(inv, "TCS_RATE")) + "% On " + N(lineTotal), "", tcs);
            if (other != 0) Sum(S(inv, "PALLETS_DESCN") == "" ? "Other Charges" : S(inv, "PALLETS_DESCN"), "", other);
            Sum("Total Amount after Tax", "", grand, true);
            tot.AddCell(Box(words, Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER));
            tot.AddCell(Box(sums, Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER));
            doc.Add(tot);

            var rc = Table(5955, 5425);
            rc.AddCell(Cell("GST Payable on Reverse Charges [Y/N]", F(ArialB, 8), Element.ALIGN_CENTER, Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER));
            rc.AddCell(Cell("NO", F(ArialB, 8), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER));
            doc.Add(rc);

            // ---- certificates, bank, signature ------------------------------------------------------------
            var cert = Table(1);
            cert.AddCell(Cell(FssText, F(Times, 8), Element.ALIGN_LEFT, Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER, 1, 3));
            cert.AddCell(Cell(PpfText, F(Times, hul ? 8 : 9), Element.ALIGN_JUSTIFIED, Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER, 1, 3));
            doc.Add(cert);

            var bank = Table(6435, 4945);
            var bl = Table(1.7f, 0.1f, 4.4f);
            bl.AddCell(Cell("Our Bank Details", F(TimesB, 10), Element.ALIGN_LEFT, b, 3));
            void Bank(string label, string value)
            {
                bl.AddCell(Cell(label, F(Times, 10)));
                bl.AddCell(Cell(":", F(Arial, 8)));
                bl.AddCell(Cell(value, F(Times, 10)));
            }
            Bank("Beneficiary Name", S(inv, "BANK_BEN_NAME"));
            Bank("Account Number", S(inv, "BANK_ACNO"));
            Bank("Beneficiary Bank", S(inv, "BANK_NAME"));
            Bank("Branch", S(inv, "BANK_BRANCH"));
            Bank("Account Type", S(inv, "BANK_ACC_TYPE"));
            Bank("IFSC Code", S(inv, "BANK_IFSC"));
            bl.AddCell(Cell(UpiText, F(Times, 10), Element.ALIGN_LEFT, b, 3, 4));
            var sig = Table(1.6f, 0.7f);
            sig.AddCell(Cell("Certified that the particulars given above are true and correct", F(Times, 9), Element.ALIGN_LEFT, b, 2, 6));
            sig.AddCell(Cell("For " + company, F(TimesB, 10), Element.ALIGN_LEFT, b, 2, 6));
            sig.AddCell(new PdfPCell(new Phrase(" ")) { Border = b, Colspan = 2, FixedHeight = 42 });
            sig.AddCell(Cell("Authorised Signatory", F(TimesB, 10)));
            sig.AddCell(Cell("[E&OE]", F(Times, 8), Element.ALIGN_RIGHT));
            bank.AddCell(Box(bl, Rectangle.LEFT_BORDER | Rectangle.BOTTOM_BORDER));
            bank.AddCell(Box(sig, Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER | Rectangle.BOTTOM_BORDER));
            doc.Add(bank);
        }

        private static Image Logo(string file)
        {
            try
            {
                var path = System.Web.Hosting.HostingEnvironment.MapPath("~/Content/logos/" + file);
                if (path == null || !File.Exists(path)) return null;
                return Image.GetInstance(path);
            }
            catch { return null; }
        }

        private static PdfPCell LogoCell(Image img)
        {
            if (img == null) return new PdfPCell(new Phrase(" ")) { Border = Rectangle.NO_BORDER };
            return new PdfPCell(img, true) { Border = Rectangle.NO_BORDER, Padding = 4, HorizontalAlignment = Element.ALIGN_CENTER, VerticalAlignment = Element.ALIGN_MIDDLE };
        }

        private static PdfPTable Party(JToken inv, string p, string caption)
        {
            var t = Table(1.1f, 1.6f, 1.3f, 0.4f);
            t.AddCell(Cell(caption, F(ArialB, 10), Element.ALIGN_LEFT, Rectangle.BOTTOM_BORDER, 4));
            t.AddCell(Cell(S(inv, p + "_NAME"), F(Arial, 9), Element.ALIGN_LEFT, Rectangle.NO_BORDER, 4));
            if (S(inv, p + "_CNAME") != "") t.AddCell(Cell("Legal Name : " + S(inv, p + "_CNAME"), F(Arial, 9), Element.ALIGN_LEFT, Rectangle.NO_BORDER, 4));
            foreach (var k in new[] { "_ADD1", "_ADD2", "_ADD3", "_CITY" })
                if (S(inv, p + k) != "") t.AddCell(Cell(S(inv, p + k), F(Arial, 9), Element.ALIGN_LEFT, Rectangle.NO_BORDER, 4));
            var pin = (S(inv, p + "_PIN") != "" ? "PIN : " + S(inv, p + "_PIN") + "        " : "") + (S(inv, p + "_PAN") != "" ? "PAN NO : " + S(inv, p + "_PAN") : "");
            if (pin != "") t.AddCell(Cell(pin, F(Arial, 9), Element.ALIGN_LEFT, Rectangle.NO_BORDER, 4));
            if (S(inv, p + "_TMC") != "") t.AddCell(Cell("TOMCO NO : " + S(inv, p + "_TMC"), F(Arial, 9), Element.ALIGN_LEFT, Rectangle.NO_BORDER, 4));
            t.AddCell(Cell("GSTIN / UNIQUE ID : " + S(inv, p + "_GST"), F(Arial, 9), Element.ALIGN_LEFT, Rectangle.NO_BORDER, 4));
            t.AddCell(Cell("State : " + S(inv, p + "_STATE"), F(ArialB, 9), Element.ALIGN_LEFT, Rectangle.TOP_BORDER, 2));
            t.AddCell(Cell("STATE CODE", F(ArialB, 8), Element.ALIGN_LEFT, Rectangle.TOP_BORDER | Rectangle.LEFT_BORDER));
            t.AddCell(Cell(S(inv, p + "_STCODE"), F(ArialB, 8), Element.ALIGN_LEFT, Rectangle.TOP_BORDER | Rectangle.LEFT_BORDER));
            return t;
        }

        private static PdfPTable Lines(float[] widths, string[] heads, IEnumerable<string[]> rows, string[] total, int[] numericCols)
        {
            var t = Table(widths);
            var hf = F(TimesB, 7.5f);
            foreach (var hd in heads)
                t.AddCell(new PdfPCell(new Phrase(hd, hf)) { HorizontalAlignment = Element.ALIGN_CENTER, VerticalAlignment = Element.ALIGN_MIDDLE, BorderWidth = 0.5f, Padding = 2 });
            t.HeaderRows = 1;
            var df = F(Times, 7.5f);
            foreach (var r in rows)
                for (int i = 0; i < r.Length; i++)
                    t.AddCell(new PdfPCell(new Phrase(r[i], df))
                    {
                        HorizontalAlignment = numericCols.Contains(i) ? Element.ALIGN_RIGHT : Element.ALIGN_LEFT,
                        Border = Rectangle.LEFT_BORDER | Rectangle.RIGHT_BORDER, BorderWidth = 0.5f, Padding = 2
                    });
            var tf = F(TimesB, 7.5f);
            for (int i = 0; i < total.Length; i++)
                t.AddCell(new PdfPCell(new Phrase(total[i], tf))
                {
                    HorizontalAlignment = numericCols.Contains(i) ? Element.ALIGN_RIGHT : Element.ALIGN_LEFT,
                    Border = Rectangle.BOX, BorderWidth = 0.5f, Padding = 2
                });
            return t;
        }

        private static PdfPTable JstLines(JArray lines)
        {
            bool igst = lines.Any(l => D(l, "IGST_RT") > 0);
            var heads = new[] { "Sl\nNo.", "LOTNO/BATCH/ MARK", "Packing\nmemo no.", "Grade", "No of\npkgs.", "Total Net\nWeight\n(Kgs)", "Rate\n(Rs.)",
                                "Amount\n(Rs.)", "Disc.\nRate (%)", "Discount\nAmount (Rs.)", "Taxable\nValue (Rs.)", (igst ? "IGST" : "CGST") + "\nRate (%)",
                                (igst ? "IGST" : "CGST") + "\nAmount (Rs.)", "SGST\nRate (%)", "SGST\nAmount (Rs.)", "Total\n(Rs.)" };
            var rows = lines.Select(l => new[]
            {
                N(D(l, "SL_NO"), 0), S(l, "MARK"), S(l, "INVNO"), S(l, "GRADE"), N(D(l, "BAG_CHEST"), 0), N(D(l, "TNET_WT")), N(D(l, "RATE")),
                N(D(l, "AMOUNT")), Nz(D(l, "DISC_PER")), Nz(D(l, "DISCOUNT")), N(D(l, "AMOUNT") - D(l, "DISCOUNT")),
                N(igst ? D(l, "IGST_RT") : D(l, "CGST_RT")), N(igst ? D(l, "IGST_AMT") : D(l, "CGST_AMT")), N(D(l, "SGST_RT")), N(D(l, "SGST_AMT")), N(D(l, "TOT_AMT"))
            });
            var total = new[]
            {
                "", "Total", "", "", N(lines.Sum(l => D(l, "BAG_CHEST")), 0), N(lines.Sum(l => D(l, "TNET_WT"))), "", N(lines.Sum(l => D(l, "AMOUNT"))), "",
                N(lines.Sum(l => D(l, "DISCOUNT"))), N(lines.Sum(l => D(l, "AMOUNT") - D(l, "DISCOUNT"))), "",
                N(lines.Sum(l => igst ? D(l, "IGST_AMT") : D(l, "CGST_AMT"))), "", N(lines.Sum(l => D(l, "SGST_AMT"))), N(lines.Sum(l => D(l, "TOT_AMT")))
            };
            return Lines(new float[] { 245, 1630, 700, 900, 375, 750, 745, 825, 370, 600, 935, 380, 745, 415, 740, 820 }, heads, rows, total,
                         new[] { 0, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 });
        }

        private static PdfPTable HulLines(JArray lines)
        {
            var heads = new[] { "Sl\nNo.", "MARK", "Invoice", "Lot No", "Grade", "Warehouse\nCode", "Serial No", "Pkg", "Net\nWt", "Gross\nWt",
                                "Sht/Sam\nSl No.", "Total Net\nWeight\n(Kgs)", "Rate\n(Rs.)", "Taxable\nValue (Rs.)" };
            var rows = lines.Select(l => new[]
            {
                N(D(l, "SL_NO"), 0), S(l, "MARK"), S(l, "INVNO"), S(l, "LOTNO"), S(l, "GRADE"), S(l, "BUYER_WH_CODE"),
                N(D(l, "CHESTSLF"), 0) + "-" + N(D(l, "CHESTSLT"), 0), N(D(l, "BAG_CHEST"), 0), N(D(l, "NET_WT")), N(D(l, "GROSS_WT")),
                S(l, "SAMPLE"), N(D(l, "TNET_WT")), N(D(l, "RATE")), N(D(l, "AMOUNT") - D(l, "DISCOUNT"))
            });
            var total = new[]
            {
                "", "", "Total", "", "", "", "", N(lines.Sum(l => D(l, "BAG_CHEST")), 0), "", "", "", N(lines.Sum(l => D(l, "TNET_WT"))), "",
                N(lines.Sum(l => D(l, "AMOUNT") - D(l, "DISCOUNT")))
            };
            return Lines(new float[] { 290, 1995, 760, 720, 960, 720, 900, 465, 610, 650, 760, 800, 700, 880 }, heads, rows, total,
                         new[] { 0, 7, 8, 9, 11, 12, 13 });
        }

        private static PdfPTable StdLines(JArray lines)
        {
            var heads = new[] { "Sl\nNo.", "Packing\nmemo no.", "Grade", "No of\npkgs.", "UOM", "Total Net\nWeight (Kgs)", "Rate", "Taxable\nValue",
                                "CGST\nRate", "CGST\nAmount", "SGST\nRate", "SGST\nAmount", "IGST\nRate", "IGST\nAmount", "Total" };
            var rows = lines.Select(l => new[]
            {
                N(D(l, "SL_NO"), 0), S(l, "INVNO"), S(l, "GRADE"), N(D(l, "BAG_CHEST"), 0), S(l, "UNIT_CD"), N(D(l, "TNET_WT")), N(D(l, "RATE")),
                N(D(l, "AMOUNT") - D(l, "DISCOUNT")), N(D(l, "CGST_RT")) + "%", N(D(l, "CGST_AMT")), N(D(l, "SGST_RT")) + "%", N(D(l, "SGST_AMT")),
                N(D(l, "IGST_RT")) + "%", N(D(l, "IGST_AMT")), N(D(l, "TOT_AMT"))
            });
            var total = new[]
            {
                "", "Total", "", N(lines.Sum(l => D(l, "BAG_CHEST")), 0), "", N(lines.Sum(l => D(l, "TNET_WT"))), "",
                N(lines.Sum(l => D(l, "AMOUNT") - D(l, "DISCOUNT"))), "", N(lines.Sum(l => D(l, "CGST_AMT"))), "", N(lines.Sum(l => D(l, "SGST_AMT"))),
                "", N(lines.Sum(l => D(l, "IGST_AMT"))), N(lines.Sum(l => D(l, "TOT_AMT")))
            };
            return Lines(new float[] { 375, 785, 1125, 470, 525, 920, 750, 1270, 480, 770, 470, 740, 505, 775, 1040 }, heads, rows, total,
                         new[] { 0, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 });
        }

        // ---- amount in words (Indian numbering) ----------------------------------------------------------
        private static readonly string[] Ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve",
                                                  "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        private static readonly string[] Tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

        private static string Below100(long n) => n < 20 ? Ones[n] : Tens[n / 10] + (n % 10 > 0 ? " " + Ones[n % 10] : "");
        private static string Below1000(long n) =>
            n >= 100 ? Ones[n / 100] + " Hundred" + (n % 100 > 0 ? " " + Below100(n % 100) : "") : Below100(n);

        public static string Words(long n)
        {
            if (n == 0) return "Zero";
            var parts = new List<string>();
            long crore = n / 10000000; n %= 10000000;
            long lakh = n / 100000; n %= 100000;
            long thousand = n / 1000; n %= 1000;
            if (crore > 0) parts.Add(Words(crore) + " Crore");
            if (lakh > 0) parts.Add(Below100(lakh) + " Lakh");
            if (thousand > 0) parts.Add(Below100(thousand) + " Thousand");
            if (n > 0) parts.Add(Below1000(n));
            return string.Join(" ", parts);
        }

        public static string AmountInWords(decimal amount)
        {
            bool neg = amount < 0;
            amount = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
            long rupees = (long)Math.Floor(amount);
            int paise = (int)((amount - rupees) * 100);
            var s = "Rupees " + Words(rupees) + (paise > 0 ? " and Paise " + Words(paise) : "") + " Only.";
            return neg ? "Minus " + s : s;
        }

        private sealed class Footer : PdfPageEventHelper
        {
            public override void OnEndPage(PdfWriter writer, Document document)
            {
                var cb = writer.DirectContent;
                var y = document.BottomMargin - 14;
                cb.SetLineWidth(0.5f);
                cb.MoveTo(document.LeftMargin, y + 10);
                cb.LineTo(document.PageSize.Width - document.RightMargin, y + 10);
                cb.Stroke();
                ColumnText.ShowTextAligned(cb, Element.ALIGN_LEFT, new Phrase("\"Printed through Classic\"", F(Arial, 8)), document.LeftMargin, y, 0);
                ColumnText.ShowTextAligned(cb, Element.ALIGN_RIGHT, new Phrase("Page " + writer.PageNumber, F(Arial, 8)),
                    document.PageSize.Width - document.RightMargin, y, 0);
            }
        }
    }
}
