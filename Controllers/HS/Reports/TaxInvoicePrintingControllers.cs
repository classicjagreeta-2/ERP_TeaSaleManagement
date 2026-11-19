using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using TeaSaleManagement.Models;
using TeaSaleManagement.Models.Report;
using TeaSaleManagement.Models.Report.HoSales;

namespace TeaSaleManagement.Controllers
{
    /// <summary>
    /// Enquiries &amp; Reports > Sales > Tax Invoice Printing (03020100T) -- old HO Sales TaxInvoicePrintingController
    /// (RepoChoice HO). Form: Unit, Sales Type, period (filters the invoice list), Invoice No(s), With Broker Name,
    /// No of Copies. Data: API HoTaxInvoicePrint/Print; print: Models\Report\HoSales\TaxInvoicePdf.
    /// "Export Invoice Printing (TT)" (03020900T, RepoChoice TT) moved to the TS module on 2026-11-10
    /// (ERP_PacketSalesExport, ExportInvoicePrintingController); its HS menu row is hidden.
    /// </summary>
    public class TaxInvoicePrintingController : HoReportControllerBase
    {
        protected override string Screen => "TaxInvoicePrinting";
        protected override string Title => "Tax Invoice Printing";
        protected override string Api => "/api/HoTaxInvoicePrint/";
        protected override string[] Lookups => new[] { "GetSaleTypes", "GetInvoices" };

        [HttpPost]
        public async Task<ActionResult> Report(string unit, string saleType, string invoices, bool withBroker = true, string copies = "All")
        {
            if (!CanView) return Denied();
            try
            {
                var invList = Codes(invoices);
                if (string.IsNullOrWhiteSpace(unit)) return Message("Select a Unit.");
                if (string.IsNullOrWhiteSpace(saleType)) return Message("Select the Sales Type.");
                if (invList.Count == 0) return Message("Select the Invoice No(s).");
                var copyList = Codes(copies).Select(c => int.TryParse(c, out var n) ? n : 0).Where(n => n >= 1 && n <= 4).Distinct().ToList();
                if (copyList.Count == 0) copyList = new List<int> { 1, 2, 3, 4 };

                var data = await PostApi("Print", new { Tt = false, SaleType = saleType, Unit = unit, Invoices = invList, WithBroker = withBroker });
                var list = (data["invoices"] as JArray) ?? new JArray();
                if (list.Count == 0) return Message("No record found for the selected criteria.");

                var company = (Utility.SessionHelper.GetUser()?.CurentCompany ?? "").Trim();

                // case-insensitive string read from a JObject
                Func<JToken, string, string> pick = (t, name) =>
                    ((((t as JObject)?.GetValue(name, StringComparison.OrdinalIgnoreCase)) ?? null)?.ToString() ?? "").Trim();

                // ---- Bank details from the finance database ----
                var bankCodes = list.Select(x => pick(x, "BANK_ACODE")).Where(x => x != "").Distinct().ToList();
                var banks = new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
                System.Diagnostics.Debug.WriteLine("bankCodes: " + string.Join(",", bankCodes));

                if (bankCodes.Count > 0)
                {
                    try
                    {
                        var res = await Services.FinanceGetAsync<JToken>(
                            $"{Api}GetBankDetail?codes={Uri.EscapeDataString(string.Join(",", bankCodes))}");
                        System.Diagnostics.Debug.WriteLine("finance res: " + Newtonsoft.Json.JsonConvert.SerializeObject(res));

                        // payload can be the array itself or wrapped in { data: [...] }
                        JToken payload = res.Data;
                        JArray arr = payload as JArray;
                        if (arr == null && payload is JObject po)
                            arr = (po.GetValue("data", StringComparison.OrdinalIgnoreCase) as JArray);
                        if (arr == null && payload != null && payload.Type != JTokenType.Null)
                            arr = JArray.FromObject(payload);   // last resort

                        foreach (var b in arr ?? new JArray())
                            banks[pick(b, "ACODE")] = b;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("GetBankDetail failed: " + ex);
                    }
                }
                System.Diagnostics.Debug.WriteLine("banks count: " + banks.Count);

                foreach (JObject inv in list)
                {
                    var acode = pick(inv, "BANK_ACODE");
                    JToken bk = null;
                    if (acode != "") banks.TryGetValue(acode, out bk);

                    // No static defaults: empty bank code or no finance row gives empty fields.
                    string bankName = bk == null ? "" : pick(bk, "BANK_NAME"),
                           acNo = bk == null ? "" : pick(bk, "AC_NO"),
                           ifsc = bk == null ? "" : pick(bk, "RTGS_CODE"),
                           Company = bk == null ? "" : pick(bk, "COMP_NAME"),
                           branch = bk == null ? "" : (pick(bk, "BRANCH") + " " + pick(bk, "ADD1")).Trim();

                    inv["BANK_BEN_NAME"] = bk == null ? "" : Company;
                    inv["BANK_ACNO"] = acNo;
                    inv["BANK_NAME"] = bankName;
                    inv["BANK_BRANCH"] = branch;
                    inv["BANK_ACC_TYPE"] = bk == null ? "" : "Cash Credit Account";
                    inv["BANK_IFSC"] = ifsc;
                }
                // ---- end bank details ----

                var pdf = TaxInvoicePdf.Build(list, copyList, company);
                Response.AddHeader("Content-Disposition", "inline; filename=\"" + Title + " " + DateTime.Now.ToString("ddMMMyyyy_HHmm") + ".pdf\"");
                return File(pdf, "application/pdf");
            }
            catch (InvalidOperationException ex) { return Message(ex.Message); }
        }
    }
}
