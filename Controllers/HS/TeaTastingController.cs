using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TeaSaleManagement;
using TeaSaleManagement.Models;
using TeaSaleManagement.Models.PT;
using TeaSaleManagement.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using static TeaSaleManagement.Helpers;

namespace TeaSaleManagement.Controllers
{
    // "Tea Tasting Entry" (HO Sales menu "10. Tea Sample Tasting", HS ORDERCODE 02040200T) -- ported from
    // VB6 trn_sample_tasting.frm (T_SAMPLE_TAST_HEAD / T_SAMPLE_TAST_DET): the tasting of the samples
    // received at HO (Tea Sample Receive Entry, T_SAMPLE_REC). The list's "New" row picks the Unit
    // (USER_SCHEMA_LINK), which is then locked on the entry screen; the list pages as a chunked infinite
    // scroll; Add / Edit / Delete / View + the back-date window come from AEDV (screen key "TeaTasting").
    // The API (api/TeaTasting) re-validates everything.
    [TeaSaleManagement.Utility.SessionRequired]
    public class TeaTastingController : Controller
    {
        private const string Screen = "TeaTasting";
        private const string ScreenName = "Tea Tasting Entry";
        private const string Api = "/api/TeaTasting/";

        private static string E(string s) => Uri.EscapeDataString(s ?? "");
        private AEDV Permission => AEDV.ForScreen((List<AEDV>)Session["User_AEDV"], Screen);

        private async Task<List<UnitOption>> GetUnitsForUserAsync()
        {
            var r = await Services.GetAsync<List<UnitOption>>(Api + "GetUnitsForUser");
            return (r.IsSuccessStatusCode ? r.Data : null) ?? new List<UnitOption>();
        }

        // GET: TeaTasting
        public async Task<ActionResult> Index(string searchString, int? page = 1, int pageSize = 15, string sortBy = "", string sortDir = "", string unit = "")
        {
            ViewBag.Permission = Permission;
            ViewBag.ScreenName = ScreenName;
            ViewBag.CurrentFilter = searchString;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDir = sortDir;
            ViewBag.PageSize = pageSize;

            var userUnits = await GetUnitsForUserAsync();
            ViewBag.UnitList = userUnits;
            var unitFilter = E(UnitScope.ListFilter(unit, userUnits));

            var isChunkRequest = Request.IsAjaxRequest();
            var pageNo = isChunkRequest ? Math.Max(page ?? 1, 1) : 1;
            ViewBag.Page = pageNo;
            ViewBag.RowOffset = (pageNo - 1) * pageSize;

            var response = await Services.GetAsync<JObject>(
                $"{Api}GetByPage?unit={unitFilter}&search={E(searchString)}&page={pageNo}&pageSize={pageSize}&sortBy={E(sortBy)}&sortDir={E(sortDir)}");

            var value = response?.Data?["value"];
            var list = value?["results"]?.ToObject<List<TeaTastingRow>>() ?? new List<TeaTastingRow>();
            ViewBag.RowCount = value?["rowCount"]?.Value<int>() ?? 0;
            ViewBag.Locks = await RecordLock.LocksAsync(Screen);

            if (isChunkRequest)
                return PartialView("_ListRows", (response?.IsSuccessStatusCode ?? false) ? list : new List<TeaTastingRow>());

            if (!(response?.IsSuccessStatusCode ?? false))
            {
                TempData["toastrError"] = !string.IsNullOrEmpty(response?.Message)
                    ? response.Message
                    : $"Unable to load the {ScreenName} list (API returned {response?.StatusCode}).";
            }
            return View("Index", list);
        }

        private void SetMinDocDate(AEDV permission, bool isNew)
        {
            DateTime? fyStart = null;
            string fy = Session["SelectedfinancialYear"]?.ToString();
            if (!string.IsNullOrEmpty(fy) && fy.Contains("-"))
            {
                var parts = fy.Split('-');
                string startDigits = new string(parts[0].Where(char.IsDigit).ToArray());
                if (startDigits.Length >= 4)
                    fyStart = new DateTime(int.Parse(startDigits.Substring(startDigits.Length - 4)), 4, 1);
            }
            var backDateFloor = (permission ?? new AEDV()).MinDocDate(isNew);
            var effectiveMin = fyStart.HasValue && fyStart.Value > backDateFloor ? fyStart.Value : backDateFloor;
            ViewBag.MinDocDate = effectiveMin.ToString("yyyy-MM-dd");
        }

        // GET: TeaTasting/InsertOrUpdate -- New: `unit` from the list's "New" row; Edit/View: the key.
        public async Task<ActionResult> InsertOrUpdate(string unit = "", string docno = "", string docdt = "", bool view = false)
        {
            ViewBag.ScreenName = ScreenName;
            var permission = Permission;
            ViewBag.Permission = permission;
            bool isNew = string.IsNullOrEmpty(docno);

            view = view && !isNew;
            if (view && !(permission?.View ?? false))
            {
                TempData["toastrError"] = "You do not have permission to view this entry.";
                return RedirectToAction("Index");
            }
            ViewBag.IsView = view;
            if (isNew && !(permission?.Add ?? false))
            {
                TempData["toastrError"] = "You do not have permission to add a new entry.";
                return RedirectToAction("Index");
            }
            SetMinDocDate(permission, isNew);

            if (isNew)
            {
                if (string.IsNullOrEmpty(unit))
                {
                    TempData["toastrError"] = "Select the Unit from the New row first.";
                    return RedirectToAction("Index");
                }
                var unitsTask = GetUnitsForUserAsync();
                var ctxTask = Services.GetAsync<JObject>($"{Api}GetNewContext?unit={E(unit)}");
                await Task.WhenAll(unitsTask, ctxTask);
                if (!UnitScope.IsAllowed(unit, unitsTask.Result))
                {
                    TempData["toastrError"] = $"You do not have permission for Unit {unit}.";
                    return RedirectToAction("Index");
                }
                var ctx = ctxTask.Result;
                if (!ctx.IsSuccessStatusCode || ctx.Data == null)
                {
                    TempData["toastrError"] = ctx.Message ?? "Unable to open a new document.";
                    return RedirectToAction("Index");
                }
                ViewBag.IsEdit = false;
                ViewBag.DocJson = ScriptJson.Serialize(new { head = ctx.Data, lines = new object[0] });
                return View();
            }

            var unitsTask2 = GetUnitsForUserAsync();
            var docTask = Services.GetAsync<JObject>($"{Api}GetByDocNo?unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}");
            await Task.WhenAll(unitsTask2, docTask);
            var r = docTask.Result;
            if (!r.IsSuccessStatusCode || r.Data == null)
            {
                TempData["toastrError"] = r.Message ?? "Record not found.";
                return RedirectToAction("Index");
            }
            var head = r.Data["head"];
            var docUnit = (string)head?["UNIT"];
            if (!UnitScope.IsAllowed(docUnit, unitsTask2.Result))
            {
                TempData["toastrError"] = $"You do not have permission for Unit {docUnit}.";
                return RedirectToAction("Index");
            }

            if (!view)
            {
                var editErr = AEDV.CheckAddEdit(permission, false, (DateTime?)head?["DOCDT"] ?? DateTime.Today);
                if (editErr != null)
                {
                    TempData["toastrError"] = editErr;
                    return RedirectToAction("Index");
                }
                var lockUnit = !string.IsNullOrEmpty(docUnit) ? docUnit : unit;
                var lockDate = (object)(DateTime?)head?["DOCDT"] ?? docdt;
                var lockErr = await RecordLock.AcquireAsync(Screen, lockUnit, docno, lockDate);
                if (lockErr != null)
                {
                    TempData["toastrError"] = lockErr;
                    return RedirectToAction("Index");
                }
                ViewBag.RecordLock = RecordLock.ClientConfig(Screen, lockUnit, docno, lockDate);
            }

            ViewBag.IsEdit = true;
            ViewBag.DocJson = ScriptJson.Serialize(r.Data);
            return View();
        }

        // POST: TeaTasting/Save (JSON body)
        [HttpPost]
        public async Task<ActionResult> Save()
        {
            TeaTastingData model;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                    model = JsonConvert.DeserializeObject<TeaTastingData>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                return JsonExact(new { success = false, message = "Invalid data: " + ex.Message });
            }
            if (model == null) return JsonExact(new { success = false, message = "Nothing to save." });

            bool isNew = model.IsNew ?? string.IsNullOrWhiteSpace(model.DOCNO);
            var permErr = AEDV.CheckAddEdit(Permission, isNew, model.DOCDT ?? DateTime.Today);
            if (permErr != null) return JsonExact(new { success = false, message = permErr });
            if (isNew) model.DOCNO = null;
            else if (string.IsNullOrWhiteSpace(model.DOCNO)) return JsonExact(new { success = false, message = "Tasting No is required." });

            if (!UnitScope.IsAllowed(model.UNIT, await GetUnitsForUserAsync()))
                return JsonExact(new { success = false, message = $"You do not have permission for Unit {model.UNIT}." });

            if (!isNew)
            {
                var lockErr = await RecordLock.AcquireAsync(Screen, model.UNIT, model.DOCNO, model.DOCDT);
                if (lockErr != null) return JsonExact(new { success = false, message = lockErr });
            }

            var r = await Services.PostAsync<JObject>(Api + "SaveOrUpdate", model);
            if (r.IsSuccessStatusCode && !isNew)
                await RecordLock.ReleaseAsync(Screen, model.UNIT, model.DOCNO, model.DOCDT);
            return JsonExact(new
            {
                success = r.IsSuccessStatusCode,
                message = r.IsSuccessStatusCode ? "Saved successfully." : (r.Message ?? "Save failed."),
                data = r.Data
            });
        }

        // POST: TeaTasting/Delete
        [HttpPost]
        public async Task<ActionResult> Delete(string unit, string docno, string docdt)
        {
            var delErr = AEDV.CheckDelete(Permission, TeaSaleManagement.DateText.Parse(docdt));
            if (delErr != null)
            {
                TempData["toastrError"] = delErr;
                return RedirectToAction("Index");
            }
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync()))
            {
                TempData["toastrError"] = $"You do not have permission for Unit {unit}.";
                return RedirectToAction("Index");
            }
            var lockErr = await RecordLock.AcquireAsync(Screen, unit, docno, docdt);
            if (lockErr != null)
            {
                TempData["toastrError"] = lockErr;
                return RedirectToAction("Index");
            }
            var r = await Services.PostAsync<JObject>($"{Api}Delete?unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}", new { });
            if (!r.IsSuccessStatusCode)
                await RecordLock.ReleaseAsync(Screen, unit, docno, docdt);
            TempData[r.IsSuccessStatusCode ? "toastrSuccess" : "toastrError"] =
                r.IsSuccessStatusCode ? $"{docno} deleted." : (r.Message ?? "Delete failed.");
            return RedirectToAction("Index");
        }

        // =====================================================================
        // AJAX passthroughs (lookups search every displayed column)
        // =====================================================================
        private ActionResult JsonExact(object data) => Content(JsonConvert.SerializeObject(data), "application/json");

        private ActionResult JsonExactOrSessionExpired<T>(ResponseApiModel<T> r)
        {
            if (!r.IsSuccessStatusCode)
            {
                bool isAuthFailure = string.Equals(r.StatusCode, HttpStatusCode.Unauthorized.ToString(), StringComparison.OrdinalIgnoreCase);
                Response.StatusCode = isAuthFailure ? 440 : 500;
                Response.TrySkipIisCustomErrors = true;
                return JsonExact(new
                {
                    sessionExpired = isAuthFailure,
                    message = isAuthFailure ? "Your session has expired. Please log in again."
                            : !string.IsNullOrEmpty(r.Message) ? r.Message : $"Unable to load data (API returned {r.StatusCode})."
                });
            }
            return JsonExact(r.Data);
        }

        private ActionResult PickerJson(ResponseApiModel<JToken> r, int limit, int p)
        {
            var arr = (r?.IsSuccessStatusCode == true ? r.Data as JArray : null) ?? new JArray();
            var take = limit > 0 ? limit : 10;
            var page = arr.Skip((Math.Max(p, 1) - 1) * take).Take(take);
            return JsonExact(new { data = page, count = arr.Count });
        }

        private async Task<ActionResult> Picker(string path, string q, string value, int limit, int p, string extra = "")
        {
            var arg = string.IsNullOrWhiteSpace(q) && !string.IsNullOrWhiteSpace(value)
                ? "code=" + E(value.Trim())
                : "search=" + E((q ?? "").Trim());
            return PickerJson(await Services.GetAsync<JToken>($"{Api}{path}?{arg}{extra}"), limit, p);
        }

        [HttpGet]
        public async Task<ActionResult> GetRowLines(string unit = "", string docno = "", string docdt = "")
        {
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync()))
                return JsonExact(new { details = new object[0] });
            return JsonExactOrSessionExpired(await Services.GetAsync<JToken>(
                $"{Api}GetRowLines?unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}"));
        }

        [HttpGet] public Task<ActionResult> GetGarden(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1, string unit = "") =>
            Picker("GetGarden", q, value, limit, p, "&unit=" + E(unit));
        [HttpGet] public Task<ActionResult> GetMark(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1, string garden = "") =>
            Picker("GetMark", q, value, limit, p, "&garden=" + E(garden));
        [HttpGet] public Task<ActionResult> GetCategory(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetCategory", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetTastingCategory(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetTastingCategory", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetTaster(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetTaster", q, value, limit, p);

        // VB6 GardenInv_List: the received samples (T_SAMPLE_REC) of the Unit not tasted yet.
        [HttpGet]
        public async Task<ActionResult> GetInvoiceList(string unit = "", string mark = "", string category = "",
                                                       string fromDate = "", string toDate = "", bool useDates = false)
        {
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync()))
                return JsonExact(new object[0]);
            return JsonExactOrSessionExpired(await Services.GetAsync<JToken>(
                $"{Api}GetInvoiceList?unit={E(unit)}&mark={E(mark)}&category={E(category)}&fromDate={E(fromDate)}&toDate={E(toDate)}&useDates={(useDates ? "true" : "false")}"));
        }
    }

    // One list row = one tasting (T_SAMPLE_TAST_HEAD).
    public class TeaTastingRow
    {
        public string UNIT { get; set; }
        public string DOCNO { get; set; }
        public DateTime? DOCDT { get; set; }
        public string GARDEN { get; set; }
        public string GARDEN_NAME { get; set; }
        public string MARK { get; set; }
        public string MARK_NAME { get; set; }
        public string TASTER_CODE { get; set; }
        public string TASTER_NAME { get; set; }
        public decimal? LINES { get; set; }
        public decimal? BAGS { get; set; }
        public decimal? TNET_WT { get; set; }
    }

    public class TeaTastingData
    {
        public string UNIT { get; set; }
        public string DOCNO { get; set; }
        public DateTime? DOCDT { get; set; }
        public string GARDEN { get; set; }
        public string MARK { get; set; }
        public string LEAF_DESC { get; set; }
        public string INFUSION_DESC { get; set; }
        public string LIQUIOR_DESC { get; set; }
        public string TASTER_CODE { get; set; }
        public bool? IsNew { get; set; }
        public List<TeaTastingLine> LINES { get; set; } = new List<TeaTastingLine>();
    }

    public class TeaTastingLine
    {
        public string INVNO { get; set; }
        public DateTime? PACK_DATE { get; set; }
        public string MARK { get; set; }
        public string GRADE { get; set; }
        public string TAST_CAT { get; set; }
        public decimal? RATE { get; set; }
        public string TAST_CAT_B { get; set; }
        public string RATE_B { get; set; }
    }
}
