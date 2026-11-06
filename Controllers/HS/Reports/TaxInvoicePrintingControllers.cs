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
    /// Reports > Sales > Tax Invoice Printing (03020100T) and Export Invoice Printing (TT) (03020900T) -- old HO Sales
    /// TaxInvoicePrintingController (RepoChoice HO / TT). Form: Unit, Sales Type, period (filters the invoice list),
    /// Invoice No(s), With Broker Name, No of Copies. Data: API HoTaxInvoicePrint/Print; print: Models\Report\HoSales\TaxInvoicePdf.
    /// Each menu row keeps its own AEDV row (screen key = controller name).
    /// </summary>
    public abstract class TaxInvoicePrintControllerBase : HoReportControllerBase
    {
        protected abstract bool Tt { get; }
        protected override string Api => "/api/HoTaxInvoicePrint/";
        protected override string[] Lookups => new[] { "GetSaleTypes", "GetInvoices" };

        public override async Task<ActionResult> Index()
        {
            ViewBag.Tt = Tt;
            ViewBag.Controller = Screen;
            var r = await base.Index();
            return r is ViewResult ? View("~/Views/TaxInvoicePrinting/Index.cshtml") : r;
        }

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

                var data = await PostApi("Print", new { Tt, SaleType = saleType, Unit = unit, Invoices = invList, WithBroker = withBroker });
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

    public class TaxInvoicePrintingController : TaxInvoicePrintControllerBase
    {
        protected override bool Tt => false;
        protected override string Screen => "TaxInvoicePrinting";
        protected override string Title => "Tax Invoice Printing";
    }

    public class ExportInvoicePrintingController : TaxInvoicePrintControllerBase
    {
        protected override bool Tt => true;
        protected override string Screen => "ExportInvoicePrinting";
        protected override string Title => "Tax Invoice Printing(TT)";
    }
}
