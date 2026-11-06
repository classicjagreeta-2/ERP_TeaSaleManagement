using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using TeaSaleManagement.Models;
using TeaSaleManagement.Models.PT;
using TeaSaleManagement.Models.Report;
using static TeaSaleManagement.Helpers;

namespace TeaSaleManagement.Controllers
{
    /// <summary>
    /// Shared plumbing of the HS report screens ported from the old HO Sales .NET app (menu "Reports > Sales"
    /// and "MIS"). The API (ClassicERPCoreAPI Controllers\TeaSale\Report) reads the rows; the screens here lay
    /// them out through Models\Report (Graphics / HTML / Excel / Excel Checklist) or, for the printed invoices,
    /// Models\Report\HoSales.
    /// Rights: the screen's own USRACS_HOSALES row (AEDV.ForScreen); a report needs the View right -- checked
    /// on the form and again on every output.
    /// </summary>
    public abstract class HoReportControllerBase : Controller
    {
        /// <summary>AEDV key = VBMENU_DONE.CONTROLLER of the menu row.</summary>
        protected abstract string Screen { get; }
        /// <summary>API controller path, e.g. "/api/HoSoudaBook/".</summary>
        protected abstract string Api { get; }
        /// <summary>Title of the form and the report.</summary>
        protected abstract string Title { get; }
        /// <summary>API picker actions the form may call through <see cref="Lookup"/>.</summary>
        protected virtual string[] Lookups => new string[0];

        protected AEDV Permission => AEDV.ForScreen((List<AEDV>)Session["User_AEDV"], Screen);
        protected bool CanView => Permission?.View ?? false;

        protected static string E(string s) => Uri.EscapeDataString(s ?? "");

        // GET: <Screen>
        public virtual async Task<ActionResult> Index()
        {
            if (!CanView)
            {
                TempData["toastrError"] = "You do not have permission to view this report.";
                return RedirectToAction("Index", "Menu");
            }
            ViewBag.Permission = Permission;
            ViewBag.Title = Title;
            ViewBag.FinancialYearFrom = "";
            ViewBag.FinancialYearTo = "";
            ViewBag.DefaultDate = DateTime.Today.ToString("dd/MM/yyyy");
            ViewBag.Loca = "";
            var ctx = await Services.GetAsync<JObject>(Api + "GetContext");
            if (ctx.IsSuccessStatusCode && ctx.Data != null)
            {
                ViewBag.FinancialYearFrom = (string)ctx.Data["FY_START"] ?? "";
                ViewBag.FinancialYearTo = (string)ctx.Data["FY_END"] ?? "";
                ViewBag.DefaultDate = (string)ctx.Data["TODAY"] ?? ViewBag.DefaultDate;
                ViewBag.Loca = (string)ctx.Data["LOCA"] ?? "";
            }
            else
            {
                TempData["toastrError"] = string.IsNullOrEmpty(ctx.Message) ? "Unable to read the financial year." : ctx.Message;
            }
            ViewBag.UnitList = await GetUnitsForUserAsync();
            return View("Index");
        }

        /// <summary>The user's units (USER_SCHEMA_LINK) -- never "all units".</summary>
        protected async Task<List<UnitOption>> GetUnitsForUserAsync()
        {
            var r = await Services.GetAsync<List<UnitOption>>(Api + "GetUnitsForUser");
            return (r.IsSuccessStatusCode ? r.Data : null) ?? new List<UnitOption>();
        }

        /// <summary>A picker list of the API (one of <see cref="Lookups"/>), every query-string value passed on.</summary>
        public async Task<ActionResult> Lookup(string what)
        {
            if (!CanView) return Json(new { error = "You do not have permission to view this report." }, JsonRequestBehavior.AllowGet);
            if (string.IsNullOrEmpty(what) || !Lookups.Contains(what, StringComparer.OrdinalIgnoreCase))
                return Json(new { error = "Unknown list." }, JsonRequestBehavior.AllowGet);
            var qs = string.Join("&", Request.QueryString.AllKeys
                .Where(k => k != null && !k.Equals("what", StringComparison.OrdinalIgnoreCase) && k != "_")
                .Select(k => E(k) + "=" + E(Request.QueryString[k])));
            var r = await Services.GetAsync<JObject>(Api + what + (qs == "" ? "" : "?" + qs));
            if (!r.IsSuccessStatusCode || r.Data == null)
                return Json(new { error = string.IsNullOrEmpty(r.Message) ? "Unable to load the list." : r.Message }, JsonRequestBehavior.AllowGet);
            return new JsonResult { Data = r.Data["data"] ?? new JArray(), JsonRequestBehavior = JsonRequestBehavior.AllowGet, MaxJsonLength = int.MaxValue }
                .WithJToken();
        }

        /// <summary>The user's units for the form's Unit picker.</summary>
        public async Task<ActionResult> Units()
        {
            if (!CanView) return Json(new { error = "You do not have permission to view this report." }, JsonRequestBehavior.AllowGet);
            var units = await GetUnitsForUserAsync();
            return Json(units.Select(u => new { CODE = u.CODE, DESCN = u.NAME }), JsonRequestBehavior.AllowGet);
        }

        /// <summary>Posts to the API; returns the JSON or throws <see cref="InvalidOperationException"/> with the API's message.</summary>
        protected async Task<JObject> PostApi(string action, object body)
        {
            var r = await Services.PostAsync<JObject>(Api + action, body);
            if (!r.IsSuccessStatusCode || r.Data == null)
                throw new InvalidOperationException(string.IsNullOrEmpty(r.Message) ? "The report could not be read (" + r.StatusCode + ")." : r.Message);
            return r.Data;
        }

        protected ActionResult Message(string message) => ReportOutputResult.Message(Title, message);
        protected ActionResult Denied() => Message("You do not have permission to view this report.");

        /// <summary>"All" / blank = empty list; else the comma-separated codes.</summary>
        protected static List<string> Codes(string picked) =>
            (picked ?? "").Split(',').Select(c => c.Trim())
                .Where(c => c != "" && !c.Equals("All", StringComparison.OrdinalIgnoreCase)).Distinct().ToList();

        protected static bool TryDate(string s, out DateTime d) =>
            DateTime.TryParseExact((s ?? "").Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

        // ---- reading the API rows ----
        protected static string Str(JToken t, string key) => ((string)t?[key] ?? "").Trim();
        protected static decimal Dec(JToken t, string key)
        {
            var v = t?[key];
            if (v == null || v.Type == JTokenType.Null) return 0m;
            try { return v.Value<decimal>(); } catch { return 0m; }
        }
        protected static decimal? DecN(JToken t, string key)
        {
            var v = t?[key];
            if (v == null || v.Type == JTokenType.Null) return null;
            try { return v.Value<decimal>(); } catch { return null; }
        }
        protected static DateTime? Dt(JToken t, string key)
        {
            var v = t?[key];
            if (v == null || v.Type == JTokenType.Null) return null;
            if (v.Type == JTokenType.Date) return v.Value<DateTime>();
            return DateTime.TryParse((string)v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null;
        }
        protected static string Dmy(DateTime? d) => d.HasValue ? d.Value.ToString("dd/MM/yyyy") : "";
        protected static IEnumerable<JToken> Rows(JObject data, string key = "rows") => (data?[key] as JArray) ?? new JArray();
    }

    internal static class JsonResultJTokenExtensions
    {
        /// <summary>JsonResult would serialise a JToken as nested empty arrays (JavaScriptSerializer); send its JSON text instead.</summary>
        public static ActionResult WithJToken(this JsonResult r)
        {
            var token = r.Data as JToken;
            if (token == null) return r;
            return new ContentResult { Content = token.ToString(Newtonsoft.Json.Formatting.None), ContentType = "application/json", ContentEncoding = System.Text.Encoding.UTF8 };
        }
    }
}
