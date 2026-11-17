using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
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
                var pdf = TaxInvoicePdf.Build(list, copyList, company);
                Response.AddHeader("Content-Disposition", "inline; filename=\"" + Title + " " + DateTime.Now.ToString("ddMMMyyyy_HHmm") + ".pdf\"");
                return File(pdf, "application/pdf");
            }
            catch (InvalidOperationException ex) { return Message(ex.Message); }
        }
    }
}
