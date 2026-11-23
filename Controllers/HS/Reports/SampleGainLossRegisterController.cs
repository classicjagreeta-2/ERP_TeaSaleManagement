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
    /// Sample Tea > Enquiries &amp; Reports > Sample Gain Loss Register (05020200T) -- old HO Sales Con_HO_04012100FController
    /// (Excel only). Rows: API HoSampleGainLoss/Register -- sample letters (sample draws), sale bills (sample qty),
    /// packings (gain / loss) and AWR shortages of the period, for the picked Type and Units. Same columns as the old
    /// Excel (Sample Letter No / Date, Packing No / Date, Blend No, Sale Bill No, Garden, Inv No, Sample, Gain, Loss,
    /// AWR No / Date, Short Qty, Mfg Date) with the grand total; now also as Graphics (PDF) and HTML.
    /// </summary>
    public class SampleGainLossRegisterController : HoReportControllerBase
    {
        protected override string Screen => "SampleGainLossRegister";
        protected override string Api => "/api/HoSampleGainLoss/";
        protected override string Title => "Sample Gain Loss Register";
        protected override string[] Lookups => new[] { "GetTypes", "GetUnits" };

        [HttpPost]
        public async Task<ActionResult> Report(string fromDate, string toDate, string units = "All", string type = "", string outputType = "Excel")
        {
            if (!CanView) return Denied();
            try
            {
                if (!TryDate(fromDate, out var from) || !TryDate(toDate, out var to)) return Message("Enter the From / To Date (dd/mm/yyyy).");
                var data = await PostApi("Register", new { FromDate = fromDate, ToDate = toDate, Units = Codes(units), Type = (type ?? "").Trim() });
                var rows = Rows(data).ToList();
                if (rows.Count == 0) return Message("No record found.");

                var unitText = string.Join(", ", ((data["units"] as JArray) ?? new JArray()).Select(u => (string)u));
                var doc = ReportDocument.Create("Sample Gain Loss Register",
                    $"From {from:dd/MM/yyyy} To {to:dd/MM/yyyy}    Type : {(string)data["typeName"]}    Unit : {unitText}", landscape: true);
                var s = doc.AddSection(null,
                    ReportColumn.Text("Sample Letter No", 0.8), ReportColumn.Date("Sample Letter Date", 0.7), ReportColumn.Text("Packing No", 0.7),
                    ReportColumn.Date("Packing Date", 0.7), ReportColumn.Text("Blend No", 0.8), ReportColumn.Text("Sale Bill No", 1.0),
                    ReportColumn.Text("Garden", 1.4), ReportColumn.Text("Inv No", 0.7), ReportColumn.Number("Sample (Kg)", 2, 0.7),
                    ReportColumn.Number("Gain (Kg)", 2, 0.7), ReportColumn.Number("Loss (Kg)", 2, 0.7), ReportColumn.Text("AWR No", 0.9),
                    ReportColumn.Date("AWR Date", 0.7), ReportColumn.Number("Short Qty", 2, 0.7), ReportColumn.Date("Mfg Date", 0.7));
                foreach (var r in rows)
                    s.Add(Str(r, "SAMPLE_LETTER_NO"), Dt(r, "SAMPLE_LETTER_DATE"), Str(r, "PACKING_NO"), Dt(r, "PACKING_DATE"), Str(r, "BLEND_NO"),
                          Str(r, "SALE_BILL_NO"), Str(r, "GARDEN"), Str(r, "INVNO"), Dec(r, "SAMPLE"), Dec(r, "GAIN"), Dec(r, "LOSS"),
                          Str(r, "AWR_NO"), Dt(r, "AWR_DATE"), Dec(r, "SHORT_QTY"), Dt(r, "MFG_DATE"));
                s.AddTotal("Total", null, "", null, "", "", "", "", rows.Sum(r => Dec(r, "SAMPLE")), rows.Sum(r => Dec(r, "GAIN")),
                           rows.Sum(r => Dec(r, "LOSS")), "", null, rows.Sum(r => Dec(r, "SHORT_QTY")), null);
                return ReportOutputResult.Send(this, doc, outputType, "SampleGainLossRegister");
            }
            catch (InvalidOperationException ex) { return Message(ex.Message); }
        }
    }
}
