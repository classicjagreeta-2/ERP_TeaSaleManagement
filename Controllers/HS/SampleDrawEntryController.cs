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
    // "Sample Drawing Entry" (HS menu Sample Tea > Inputs) -- ported from VB6 trn_sample_draw.frm as the
    // HO Sales menu opens it (pTrans_Type "HO": T_SAMPLE_DRAW rows with TRAN_CODE 'HO'; samples drawn
    // from the finished stock at the Sale Centre). Copied from ERP_WarehouseManagement's
    // WarehouseSampleDraw screen (the "WH" variant); the API is api/HoSampleDraw. The list's "New" row
    // picks the Unit (USER_SCHEMA_LINK), locked on the entry screen; chunked infinite-scroll list; AEDV
    // screen key "SampleDrawEntry". Sample Gate Pass Printing (Sample Tea > Enquiries & Reports) prints
    // these documents.
    [TeaSaleManagement.Utility.SessionRequired]
    public class SampleDrawEntryController : Controller
    {
        private const string Screen = "SampleDrawEntry";   // AEDV.ForScreen key and record-lock screen
        private const string ScreenName = "Sample Drawing Entry";
        private const string Api = "/api/HoSampleDraw/";

        private static string E(string s) => Uri.EscapeDataString(s ?? "");

        private AEDV Permission => AEDV.ForScreen((List<AEDV>)Session["User_AEDV"], Screen);

        // The Units the user is linked to (USER_SCHEMA_LINK) -- backs the list scope, the
        // "New" row's Unit picker and the Unit checks on open / save / delete.
        private async Task<List<UnitOption>> GetUnitsForUserAsync()
        {
            var r = await Services.GetAsync<List<UnitOption>>(Api + "GetUnitsForUser");
            return (r.IsSuccessStatusCode ? r.Data : null) ?? new List<UnitOption>();
        }

        // GET: SampleDrawEntry
        // Scoped to the Units the user is linked to (or the one `unit` asked for, if permitted),
        // sent as a comma-separated list.
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

            // Chunked list: a full page view always starts at the first chunk; further chunks
            // arrive as AJAX calls (Scripts/list-infinite-scroll.js) and return just the rows.
            var isChunkRequest = Request.IsAjaxRequest();
            var pageNo = isChunkRequest ? Math.Max(page ?? 1, 1) : 1;
            ViewBag.Page = pageNo;
            ViewBag.RowOffset = (pageNo - 1) * pageSize;

            var response = await Services.GetAsync<JObject>(
                $"{Api}GetByPage?unit={unitFilter}&search={E(searchString)}&page={pageNo}&pageSize={pageSize}&sortBy={E(sortBy)}&sortDir={E(sortDir)}");

            var value = response?.Data?["value"];
            var list = value?["results"]?.ToObject<List<SampleDrawEntryRow>>() ?? new List<SampleDrawEntryRow>();
            ViewBag.RowCount = value?["rowCount"]?.Value<int>() ?? 0;
            // Rows another user is editing are greyed out (see Utility/RecordLock.cs).
            ViewBag.Locks = await RecordLock.LocksAsync(Screen);

            if (isChunkRequest)
            {
                // A failed chunk must not leave a toast queued for the next full page load; an
                // empty response tells the list's scroll loader to stop.
                return PartialView("_ListRows", (response?.IsSuccessStatusCode ?? false) ? list : new List<SampleDrawEntryRow>());
            }

            if (!(response?.IsSuccessStatusCode ?? false))
            {
                TempData["toastrError"] = !string.IsNullOrEmpty(response?.Message)
                    ? response.Message
                    : $"Unable to load the {ScreenName} list (API returned {response?.StatusCode}).";
            }
            return View("Index", list);
        }

        // Doc Date picker's lower bound -- the later of the financial-year start and the AEDV
        // back-date allowance (Aday for a new document, Eday while editing). Save() is the real
        // gate; this only steers the date picker.
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

        // GET: SampleDrawEntry/InsertOrUpdate
        // New: `unit` comes from the list's "New" row and is locked here.
        // Edit/View: the document's key (unit, docno, docdt).
        public async Task<ActionResult> InsertOrUpdate(string unit = "", string docno = "", string docdt = "", bool view = false)
        {
            ViewBag.ScreenName = ScreenName;

            var permission = Permission;
            ViewBag.Permission = permission;

            bool isNew = string.IsNullOrEmpty(docno);

            // `view` is set by the list's View button / Doc No link -- same fetch as Edit, but the
            // form renders read-only. It only applies to an existing document and needs the View right.
            view = view && !isNew;
            if (view && !(permission?.View ?? false))
            {
                TempData["toastrError"] = "You do not have permission to view this entry.";
                return RedirectToAction("Index");
            }
            ViewBag.IsView = view;

            // The list hides New without the Add right, but this URL is reachable directly.
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
                var ctx = ctxTask.Result;

                if (!UnitScope.IsAllowed(unit, unitsTask.Result))
                {
                    TempData["toastrError"] = $"You do not have permission for Unit {unit}.";
                    return RedirectToAction("Index");
                }
                if (!ctx.IsSuccessStatusCode || ctx.Data == null)
                {
                    TempData["toastrError"] = ctx.Message ?? "Unable to open a new entry.";
                    return RedirectToAction("Index");
                }
                ViewBag.IsEdit = false;
                ViewBag.DocJson = JsonConvert.SerializeObject(new { head = ctx.Data, lines = new object[0] });
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

            // The document's own Unit must be one the user is linked to.
            var head = r.Data["head"];
            var docUnit = (string)head?["UNIT"];
            if (!UnitScope.IsAllowed(docUnit, unitsTask2.Result))
            {
                TempData["toastrError"] = $"You do not have permission for Unit {docUnit}.";
                return RedirectToAction("Index");
            }

            // Block opening Edit outright when the user has no Edit right or the document's own Doc
            // Date has fallen outside the Eday back-date window -- View bypasses this.
            if (!view)
            {
                var editErr = AEDV.CheckAddEdit(permission, false, (DateTime?)head?["DOCDT"] ?? DateTime.Today);
                if (editErr != null)
                {
                    TempData["toastrError"] = editErr;
                    return RedirectToAction("Index");
                }

                // Edit lock: refused while another user has this document open for editing.
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
            ViewBag.DocJson = JsonConvert.SerializeObject(r.Data);
            return View();
        }

        // POST: SampleDrawEntry/Save (JSON body). The API re-validates everything.
        [HttpPost]
        public async Task<ActionResult> Save()
        {
            SampleDrawEntryData model;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                    model = JsonConvert.DeserializeObject<SampleDrawEntryData>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                return JsonExact(new { success = false, message = "Invalid data: " + ex.Message });
            }
            if (model == null) return JsonExact(new { success = false, message = "Nothing to save." });
            model.SAMPLE_TAG = null;    // this menu item never tags (an edit keeps the stored tag, API-side)

            // Server-side AEDV Add/Edit + back-date enforcement -- the Doc Date picker's `min`
            // (see InsertOrUpdate) only steers well-behaved clients; this is the actual gate.
            bool isNew = model.IsNew ?? string.IsNullOrWhiteSpace(model.DOCNO);
            var permErr = AEDV.CheckAddEdit(Permission, isNew, model.DOCDT ?? DateTime.Today);
            if (permErr != null) return JsonExact(new { success = false, message = permErr });
            // A "new" document never carries a Doc No (the API numbers it); an edit always does.
            if (isNew) model.DOCNO = null;
            else if (string.IsNullOrWhiteSpace(model.DOCNO)) return JsonExact(new { success = false, message = "Doc No is required." });

            // The Unit (a NEW document is stamped with the one picked on the list, posted from the
            // entry screen; an edit keeps the document's own) must be one the user is linked to.
            if (!UnitScope.IsAllowed(model.UNIT, await GetUnitsForUserAsync()))
                return JsonExact(new { success = false, message = $"You do not have permission for Unit {model.UNIT}." });

            // Edit lock: an edit may only be saved while no other user holds the document.
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

        // POST: SampleDrawEntry/Delete
        [HttpPost]
        public async Task<ActionResult> Delete(string unit, string docno, string docdt)
        {
            var delErr = AEDV.CheckDelete(Permission, TeaSaleManagement.DateText.Parse(docdt));
            if (delErr != null)
            {
                TempData["toastrError"] = delErr;
                return RedirectToAction("Index");
            }

            // The row's Unit must be one the user is linked to.
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync()))
            {
                TempData["toastrError"] = $"You do not have permission for Unit {unit}.";
                return RedirectToAction("Index");
            }

            // Not while another user has the document open for editing.
            var lockErr = await RecordLock.AcquireAsync(Screen, unit, docno, docdt);
            if (lockErr != null)
            {
                TempData["toastrError"] = lockErr;
                return RedirectToAction("Index");
            }

            var r = await Services.PostAsync<JObject>(
                $"{Api}Delete?unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}", new { });
            if (!r.IsSuccessStatusCode)
                await RecordLock.ReleaseAsync(Screen, unit, docno, docdt);
            TempData[r.IsSuccessStatusCode ? "toastrSuccess" : "toastrError"] =
                r.IsSuccessStatusCode ? $"{docno} deleted." : (r.Message ?? "Delete failed.");
            return RedirectToAction("Index");
        }

        // =====================================================================
        // AJAX passthroughs (lookups search every displayed column)
        // =====================================================================
        private ActionResult JsonExact(object data) =>
            Content(JsonConvert.SerializeObject(data), "application/json");

        // 440 = session expired (prompts re-login), 500 = any other API failure.
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

        // Reshapes a plain-array lookup response into { data, count } for jquery.inputpicker
        // (Root UI convention -- see CLAUDE.md's "Lookup pickers" rule). The API returns up to its
        // top-100 matches, so this pages that array in memory.
        private ActionResult PickerJson(ResponseApiModel<JToken> r, int limit, int p)
        {
            var arr = (r?.IsSuccessStatusCode == true ? r.Data as JArray : null) ?? new JArray();
            var take = limit > 0 ? limit : 10;
            var page = arr.Skip((Math.Max(p, 1) - 1) * take).Take(take);
            return JsonExact(new { data = page, count = arr.Count });
        }

        // inputpicker endpoints. The plugin sends the typed text as `q` (the API matches it,
        // case-insensitively, against EVERY column the dropdown shows) and -- when the field
        // already holds a value -- that value as `value`, looked up as an exact code so a loaded
        // record's picker shows its own row. `extra` carries the picker's own filters.
        private async Task<ActionResult> Picker(string path, string q, string value, int limit, int p, string extra = "")
        {
            var arg = string.IsNullOrWhiteSpace(q) && !string.IsNullOrWhiteSpace(value)
                ? "code=" + E(value.Trim())
                : "search=" + E((q ?? "").Trim());
            return PickerJson(await Services.GetAsync<JToken>($"{Api}{path}?{arg}{extra}"), limit, p);
        }

        // "+" expand / Lines: the document's sampled lots, one lean call ({ details: [...] }).
        [HttpGet]
        public async Task<ActionResult> GetRowLines(string unit = "", string docno = "", string docdt = "")
        {
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync()))
                return JsonExact(new { details = new object[0] });
            return JsonExactOrSessionExpired(await Services.GetAsync<JToken>(
                $"{Api}GetRowLines?unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}"));
        }

        // "Select Data": the warehouse's AWR stock still available for sampling
        // ({ rows: [...], truncated, limit }). Filters as on the VB form's stock panel.
        [HttpGet]
        public async Task<ActionResult> GetStock(string warehouse = "", string saleCentre = "", string saleType = "",
                                                 string mark = "", string garden = "", string category = "", string awrNo = "",
                                                 string docDate = "", string unit = "", string docno = "", string docdt = "")
        {
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync()))
                return JsonExact(new { rows = new object[0], truncated = false, limit = 0 });
            return JsonExactOrSessionExpired(await Services.GetAsync<JToken>(
                $"{Api}GetStock?warehouse={E(warehouse)}&saleCentre={E(saleCentre)}&saleType={E(saleType)}" +
                $"&mark={E(mark)}&garden={E(garden)}&category={E(category)}&awrNo={E(awrNo)}&docDate={E(docDate)}" +
                $"&unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}"));
        }

        [HttpGet] public Task<ActionResult> GetSaleType(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetSaleType", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetSalesCentre(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetSalesCentre", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetWarehouse(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetWarehouse", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetBroker(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetBroker", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetParty(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetParty", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetSaleNo(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetSaleNo", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetGarden(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetGarden", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetCategory(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetCategory", q, value, limit, p);
        // Mark is narrowed to the Garden picked in the stock filter; AWR No to the Warehouse and the
        // Mark / Garden (sent through the picker's urlParam).
        [HttpGet] public Task<ActionResult> GetMark(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1, string garden = "") =>
            Picker("GetMark", q, value, limit, p, "&garden=" + E(garden));
        [HttpGet] public Task<ActionResult> GetAwrNo(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1,
                                                     string warehouse = "", string mark = "", string garden = "") =>
            Picker("GetAwrNo", q, value, limit, p, "&warehouse=" + E(warehouse) + "&mark=" + E(mark) + "&garden=" + E(garden));
    }

    // One list row = one document (T_SAMPLE_DRAW grouped by Unit + Doc No + Doc Date, TRAN_CODE 'HO').
    public class SampleDrawEntryRow
    {
        public string UNIT { get; set; }
        public string DOCNO { get; set; }
        public DateTime? DOCDT { get; set; }
        public string SALE_TYPE { get; set; }
        public string SALE_TYPE_NAME { get; set; }
        public string SALE_CENTRE { get; set; }
        public string SALE_CENTRE_NAME { get; set; }
        public string WAREHOUSE { get; set; }
        public string WAREHOUSE_NAME { get; set; }
        public string BROK_CODE { get; set; }
        public string BROKER_NAME { get; set; }
        public string PCODE { get; set; }
        public string PARTY_NAME { get; set; }
        public string SAMPLE_TAG { get; set; }
        public decimal? LINES { get; set; }
        public decimal? TOTAL_BAGS { get; set; }
        public decimal? TOTAL_NET { get; set; }
        public decimal? TOTAL_DRAW { get; set; }
    }

    // Body posted to Save, forwarded to POST api/HoSampleDraw/SaveOrUpdate.
    public class SampleDrawEntryData
    {
        public string UNIT { get; set; }
        public string DOCNO { get; set; }
        public DateTime? DOCDT { get; set; }
        public string SALE_TYPE { get; set; }
        public string SALE_CENTRE { get; set; }
        public string WAREHOUSE { get; set; }
        public string BROK_CODE { get; set; }
        public string PCODE { get; set; }
        public string SAMPLE_TAG { get; set; }
        public bool? IsNew { get; set; }
        public List<SampleDrawEntryLine> LINES { get; set; } = new List<SampleDrawEntryLine>();
    }

    public class SampleDrawEntryLine
    {
        public string SALE_NO { get; set; }
        public DateTime? SALE_DATE { get; set; }
        public DateTime? PROMPT_DATE { get; set; }
        public string INVNO { get; set; }
        public string MARK { get; set; }
        public string GRADE { get; set; }
        public DateTime? PACK_DATE { get; set; }
        public string AWR_NO { get; set; }
        public DateTime? AWR_DATE { get; set; }
        public string CAT { get; set; }
        public string GARDEN { get; set; }
        public decimal? BAG_CHEST { get; set; }
        public decimal? NET_WT { get; set; }
        public decimal? TNET_WT { get; set; }
        public decimal? TGROSS_WT { get; set; }
        public decimal? TARE_WT { get; set; }
        public decimal? DRAW_QTY { get; set; }
        public string LOTNO { get; set; }
        public decimal? CHEST_NO { get; set; }
        public decimal? CHESTSLF { get; set; }
        public decimal? CHESTSLT { get; set; }
        public DateTime? MFG_FROM { get; set; }
        public DateTime? MFG_TO { get; set; }
        public string SIZE_CODE { get; set; }
        public string CROP_YEAR { get; set; }
        public string UNCLAIM { get; set; }
    }
}
