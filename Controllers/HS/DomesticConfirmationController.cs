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
    // "Domestic Confirmation" -- VB6 TRN_OFFER_CONF.frm with pConf_tag "D" (VB menu 4.1 Domestic).
    // Moved here from the TS module (ERP_PacketSalesExport ConfirmationEntryController, tag D) on
    // 2026-11-04; TS no longer offers it. Same API as there: the list is ConfirmationEntry/GetByPage
    // with tag=D, everything else is the API's DomesticConfirmation controller (factory schema).
    // List and entry screen follow Sauda Entry (see CLAUDE.md): the list's "New" row picks the Unit,
    // which is then locked on the entry screen; the list is scoped to the Units the user is linked to
    // (USER_SCHEMA_LINK), pages as a chunked infinite scroll, and Add / Edit / Delete / View + the
    // back-date window come from AEDV (screen key "DomesticConfirmation").
    public class DomesticConfirmationController : Controller
    {
        private const string Screen = "DomesticConfirmation";   // AEDV.ForScreen key and record-lock screen
        private const string Tag = "D";                         // T_OFFER_CONF types of M_SALETYPE for Domestic
        private const string ListApi = "/api/ConfirmationEntry/";
        private const string Api = "/api/DomesticConfirmation/";

        private static string E(string s) => Uri.EscapeDataString(s ?? "");
        // The "type" part of the record-lock key (API RecordLockController: T_OFFER_CONF UNIT, DOCNO, DOCDT, SALE_TYPE).
        private static string LockType(string saleType) => (saleType ?? "").Trim().ToUpperInvariant();

        private AEDV Permission => AEDV.ForScreen((List<AEDV>)Session["User_AEDV"], Screen);

        // The Units the user is linked to (USER_SCHEMA_LINK) -- backs the list scope, the
        // "New" row's Unit picker and the Unit checks on open / save / delete. Never "all units".
        private async Task<List<UnitOption>> GetUnitsForUserAsync()
        {
            var r = await Services.GetAsync<List<UnitOption>>(ListApi + "GetUnitsForUser");
            return (r.IsSuccessStatusCode ? r.Data : null) ?? new List<UnitOption>();
        }

        // GET: DomesticConfirmation
        public async Task<ActionResult> Index(string searchString = "", int? page = 1, int pageSize = 15,
                                              string sortBy = "", string sortDir = "", string unit = "")
        {
            ViewBag.Permission = Permission;
            ViewBag.CurrentFilter = searchString;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDir = sortDir;
            ViewBag.PageSize = pageSize;

            var userUnits = await GetUnitsForUserAsync();
            ViewBag.UnitList = userUnits;
            // The one unit asked for (if permitted) else every unit the user is linked to;
            // "~" (matches nothing) when the user has none.
            var unitFilter = E(UnitScope.ListFilter(unit, userUnits));

            // Chunked list: a full page view always starts at the first chunk; further chunks
            // arrive as AJAX calls (Scripts/list-infinite-scroll.js) and return just the rows.
            var isChunkRequest = Request.IsAjaxRequest();
            var pageNo = isChunkRequest ? Math.Max(page ?? 1, 1) : 1;
            ViewBag.Page = pageNo;
            ViewBag.RowOffset = (pageNo - 1) * pageSize;

            var response = await Services.GetAsync<JObject>(
                $"{ListApi}GetByPage?tag={Tag}&unit={unitFilter}&search={E(searchString)}&page={pageNo}&pageSize={pageSize}&sortBy={E(sortBy)}&sortDir={E(sortDir)}");

            var value = response?.Data?["value"];
            var list = value?["results"]?.ToObject<List<DomConfRow>>() ?? new List<DomConfRow>();
            ViewBag.RowCount = value?["rowCount"]?.Value<int>() ?? 0;
            // Rows another user is editing are greyed out (see Utility/RecordLock.cs).
            ViewBag.Locks = await RecordLock.LocksAsync(Screen);

            if (isChunkRequest)
                return PartialView("_ListRows", (response?.IsSuccessStatusCode ?? false) ? list : new List<DomConfRow>());

            if (!(response?.IsSuccessStatusCode ?? false))
            {
                TempData["toastrError"] = !string.IsNullOrEmpty(response?.Message)
                    ? response.Message
                    : $"Unable to load the Domestic Confirmation list (API returned {response?.StatusCode}).";
            }
            return View("Index", list);
        }

        // Doc Date picker's lower bound -- the later of the financial-year start and the AEDV
        // back-date allowance (Aday for a new entry, Eday while editing). Save() is the real gate.
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

        // GET: DomesticConfirmation/InsertOrUpdate
        // New: `unit` comes from the list's "New" row and is locked here.
        // Edit / View: the confirmation's key (unit, docno, docdt, saleType).
        public async Task<ActionResult> InsertOrUpdate(string unit = "", string docno = "", string docdt = "", bool view = false, string saleType = "")
        {
            var permission = Permission;
            ViewBag.Permission = permission;
            ViewBag.Tag = Tag;
            ViewBag.TypeName = "Domestic";

            bool isNew = string.IsNullOrEmpty(docno);

            // View only applies to an existing confirmation and needs the View right.
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
                var ctxTask = Services.GetAsync<JObject>($"{Api}GetNewContext?tag={Tag}&unit={E(unit)}");
                await Task.WhenAll(unitsTask, ctxTask);
                var ctx = ctxTask.Result;

                if (!UnitScope.IsAllowed(unit, unitsTask.Result))
                {
                    TempData["toastrError"] = $"You do not have permission for Unit {unit}.";
                    return RedirectToAction("Index");
                }
                if (!ctx.IsSuccessStatusCode || ctx.Data == null)
                {
                    TempData["toastrError"] = ctx.Message ?? "Unable to open a new confirmation.";
                    return RedirectToAction("Index");
                }
                ViewBag.IsEdit = false;
                ViewBag.DocJson = ScriptJson.Serialize(ctx.Data);
                return View();
            }

            var unitsTask2 = GetUnitsForUserAsync();
            var docTask = Services.GetAsync<JObject>(
                $"{Api}GetByDocNo?tag={Tag}&unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}&saleType={E(saleType)}");
            await Task.WhenAll(unitsTask2, docTask);
            var r = docTask.Result;
            if (!r.IsSuccessStatusCode || r.Data == null)
            {
                TempData["toastrError"] = r.Message ?? "Record not found.";
                return RedirectToAction("Index");
            }

            // The confirmation's own Unit must be one the user is linked to (a Conf No repeats across units).
            var head = r.Data["head"];
            var docUnit = (string)head?["UNIT"];
            if (!UnitScope.IsAllowed(docUnit, unitsTask2.Result))
            {
                TempData["toastrError"] = $"You do not have permission for Unit {docUnit}.";
                return RedirectToAction("Index");
            }

            // Block opening Edit outright when the user has no Edit right, the Conf Date has fallen
            // outside the Eday back-date window, or the month is locked (VB6 edit_record) --
            // View bypasses this.
            if (!view)
            {
                var docDate = (DateTime?)head?["DOCDT"] ?? DateTime.Today;
                var editErr = AEDV.CheckAddEdit(permission, false, docDate);
                var lockDt = (DateTime?)r.Data["ctx"]?["LOCK_DATE"];
                if (editErr == null && lockDt.HasValue && docDate.Date <= lockDt.Value.Date)
                    editErr = $"Transaction Entry/Editing Locked upto {lockDt.Value:dd/MM/yyyy}";
                if (editErr != null)
                {
                    TempData["toastrError"] = editErr;
                    return RedirectToAction("Index");
                }

                // Edit lock: refused while another user has this confirmation open for editing.
                var lockUnit = !string.IsNullOrEmpty(docUnit) ? docUnit : unit;
                var lockType = LockType((string)head?["SALE_TYPE"]);
                var lockErr = await RecordLock.AcquireAsync(Screen, lockUnit, docno, docDate, lockType);
                if (lockErr != null)
                {
                    TempData["toastrError"] = lockErr;
                    return RedirectToAction("Index");
                }
                ViewBag.RecordLock = RecordLock.ClientConfig(Screen, lockUnit, docno, docDate, lockType);
            }

            ViewBag.IsEdit = true;
            ViewBag.DocJson = ScriptJson.Serialize(r.Data);
            return View();
        }

        // POST: DomesticConfirmation/Save (JSON body). The API validates everything and recomputes
        // the amounts; a NEW entry may come back as several confirmations (one per warehouse, as
        // VB6 saves it). dryRun=true runs it all and rolls back.
        [HttpPost]
        public async Task<ActionResult> Save(bool dryRun = false)
        {
            DomConfPost model;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                    model = JsonConvert.DeserializeObject<DomConfPost>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                return JsonExact(new { success = false, message = "Invalid data: " + ex.Message });
            }
            if (model == null) return JsonExact(new { success = false, message = "Nothing to save." });
            model.CONF_TAG = Tag;   // this screen saves Domestic only, whatever was posted

            // Server-side AEDV Add/Edit + back-date enforcement (the real gate).
            bool isNew = model.IsNew ?? string.IsNullOrWhiteSpace(model.DOCNO);
            var permErr = AEDV.CheckAddEdit(Permission, isNew, model.DOCDT ?? DateTime.Today);
            if (permErr != null) return JsonExact(new { success = false, message = permErr });

            // The Unit (a NEW entry carries the one picked on the list; an edit keeps its own)
            // must be one the user is linked to.
            if (!UnitScope.IsAllowed(model.UNIT, await GetUnitsForUserAsync()))
                return JsonExact(new { success = false, message = $"You do not have permission for Unit {model.UNIT}." });

            // Edit lock: an edit may only be saved while no other user holds the confirmation.
            var lockType = LockType(model.SALE_TYPE);
            if (!isNew)
            {
                if (string.IsNullOrEmpty(lockType)) return JsonExact(new { success = false, message = "Sale Type cannot be blank" });
                var lockErr = await RecordLock.AcquireAsync(Screen, model.UNIT, model.DOCNO, model.DOCDT, lockType);
                if (lockErr != null) return JsonExact(new { success = false, message = lockErr });
            }

            var r = await Services.PostAsync<JObject>(Api + "SaveOrUpdate" + (dryRun ? "?dryRun=true" : ""), model);
            bool saved = r.IsSuccessStatusCode && (r.Data == null || (bool?)r.Data["success"] != false);
            if (saved && !isNew && !dryRun)
                await RecordLock.ReleaseAsync(Screen, model.UNIT, model.DOCNO, model.DOCDT, lockType);
            return JsonExact(new
            {
                success = saved,
                message = saved ? "Saved successfully." : (r.Message ?? "Save failed."),
                data = r.Data
            });
        }

        // POST: DomesticConfirmation/Delete
        [HttpPost]
        public async Task<ActionResult> Delete(string unit, string docno, string docdt, string saleType = "")
        {
            var delErr = AEDV.CheckDelete(Permission, TeaSaleManagement.DateText.Parse(docdt));
            if (delErr != null)
            {
                TempData["toastrError"] = delErr;
                return RedirectToAction("Index");
            }

            // The row's Unit (a Conf No repeats across units) must be one the user is linked to.
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync()))
            {
                TempData["toastrError"] = $"You do not have permission for Unit {unit}.";
                return RedirectToAction("Index");
            }

            // Not while another user has the confirmation open for editing.
            saleType = LockType(saleType);
            if (string.IsNullOrEmpty(saleType))
            {
                TempData["toastrError"] = "The row's Sale Type is missing - reload the list and try again.";
                return RedirectToAction("Index");
            }
            var lockErr = await RecordLock.AcquireAsync(Screen, unit, docno, docdt, saleType);
            if (lockErr != null)
            {
                TempData["toastrError"] = lockErr;
                return RedirectToAction("Index");
            }

            var r = await Services.PostAsync<JObject>(
                $"{Api}Delete?tag={Tag}&unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}&saleType={E(saleType)}", new { });
            if (!r.IsSuccessStatusCode)
                await RecordLock.ReleaseAsync(Screen, unit, docno, docdt, saleType);
            TempData[r.IsSuccessStatusCode ? "toastrSuccess" : "toastrError"] =
                r.IsSuccessStatusCode ? $"Confirmation {docno} deleted." : (r.Message ?? "Delete failed.");
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

        private async Task<ActionResult> Lookup(string path) =>
            JsonExactOrSessionExpired(await Services.GetAsync<JToken>(Api + path));

        // Reshapes a plain-array lookup response into { data, count } for jquery.inputpicker
        // (Root UI convention -- CLAUDE.md "Lookup pickers"). The API returns up to its top-100
        // matches, so this pages that array in memory.
        private ActionResult PickerJson(ResponseApiModel<JToken> r, int limit, int p)
        {
            var arr = (r?.IsSuccessStatusCode == true ? r.Data as JArray : null) ?? new JArray();
            var take = limit > 0 ? limit : 10;
            var page = arr.Skip((Math.Max(p, 1) - 1) * take).Take(take);
            return JsonExact(new { data = page, count = arr.Count });
        }

        // inputpicker endpoints: the plugin sends the typed text as `q` (the API matches it,
        // case-insensitively, against EVERY column the dropdown shows) and -- when the field
        // already holds a value -- that value as `value`, looked up as an exact code.
        private async Task<ActionResult> Picker(string path, string q, string value, int limit, int p, string extra = "")
        {
            var arg = string.IsNullOrWhiteSpace(q) && !string.IsNullOrWhiteSpace(value)
                ? "code=" + E(value.Trim())
                : "search=" + E((q ?? "").Trim());
            return PickerJson(await Services.GetAsync<JToken>($"{Api}{path}?{arg}{extra}"), limit, p);
        }

        [HttpGet] public Task<ActionResult> GetRowLines(string unit = "", string docno = "", string docdt = "", string saleType = "") =>
            Lookup($"GetRowLines?tag={Tag}&unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}&saleType={E(saleType)}");

        // The entry screen (InsertOrUpdate.cshtml, copied from the TS screen) calls these by the TS names.
        [HttpGet] public Task<ActionResult> DomSaleType(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1, string tag = Tag, string unit = "") =>
            Picker("GetSaleType", q, value, limit, p, "&tag=" + Tag + "&unit=" + E(unit));
        [HttpGet] public Task<ActionResult> DomParty(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1, string kind = "B") =>
            Picker("GetParty", q, value, limit, p, "&kind=" + E(kind));
        [HttpGet] public Task<ActionResult> DomBroker(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetBroker", q, value, limit, p);
        [HttpGet] public Task<ActionResult> DomTransporter(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetTransporter", q, value, limit, p);
        [HttpGet] public Task<ActionResult> DomDestination(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetDestination", q, value, limit, p);
        [HttpGet] public Task<ActionResult> DomSalesCentre(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetSalesCentre", q, value, limit, p);
        [HttpGet] public Task<ActionResult> DomMark(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1, bool own = false) =>
            Picker("GetMark", q, value, limit, p, "&own=" + (own ? "true" : "false"));
        [HttpGet] public Task<ActionResult> DomGrade(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetGrade", q, value, limit, p);
        [HttpGet] public Task<ActionResult> DomGarden(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1, string tag = Tag) =>
            Picker("GetGarden", q, value, limit, p, "&tag=" + Tag);
        [HttpGet] public Task<ActionResult> DomCategory(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetCategory", q, value, limit, p);

        // "Select Data": composite, multi-select list (bespoke popup). Only for a Unit the user is linked to.
        [HttpGet]
        public async Task<ActionResult> DomStock(string tag = Tag, string unit = "", string saleType = "", string saleCentre = "", string mark = "",
                                                 string garden = "", string category = "", string dateFrom = "", string dateTo = "", bool saudaOnly = false,
                                                 string docno = "", string docdt = "")
        {
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync()))
                return JsonExact(new object[0]);
            return await Lookup($"GetStock?tag={Tag}&unit={E(unit)}&saleType={E(saleType)}&saleCentre={E(saleCentre)}&mark={E(mark)}" +
                                $"&garden={E(garden)}&category={E(category)}&dateFrom={E(dateFrom)}&dateTo={E(dateTo)}" +
                                $"&saudaOnly={(saudaOnly ? "true" : "false")}&docno={E(docno)}&docdt={E(docdt)}");
        }
    }

    // A list row (API ConfirmationEntry/GetByPage, tag D): one confirmation, totals over its lines.
    public class DomConfRow
    {
        public string CONF_TAG { get; set; }
        public string TYPE_NAME { get; set; }
        public string UNIT { get; set; }
        public string SALE_TYPE { get; set; }
        public string DOCNO { get; set; }
        public DateTime? DOCDT { get; set; }
        public string PCD { get; set; }
        public string PARTY_NAME { get; set; }
        public string SALE_CENTRE { get; set; }
        public string SALE_CENTRE_NAME { get; set; }
        public int? LINES { get; set; }
        public decimal? BAGS { get; set; }
        public decimal? ORDER_QTY { get; set; }
        public decimal? AMT { get; set; }
        public string INV_NO { get; set; }
    }

    // What InsertOrUpdate posts to Save, forwarded as-is to the API's
    // DomesticConfirmation/SaveOrUpdate (same shape as its DOM_CONF_DATA).
    public class DomConfPost
    {
        public string CONF_TAG { get; set; }
        public string UNIT { get; set; }
        public string DOCNO { get; set; }
        public DateTime? DOCDT { get; set; }
        public bool? IsNew { get; set; }          // new-vs-edit for the AEDV check (see Save)
        public string SALE_TYPE { get; set; }
        public string SALE_CENTRE { get; set; }
        public string PCD { get; set; }
        public string SHIP_PCD { get; set; }
        public string BROKER { get; set; }
        public string CUST { get; set; }
        public decimal? PROMPT_DAY { get; set; }
        public bool? CASH { get; set; }
        public string ORDNO { get; set; }
        public DateTime? ORDDT { get; set; }
        public string TPT { get; set; }
        public string DEST_TO { get; set; }
        public string DI_TAG { get; set; }
        public List<DomConfLinePost> LINES { get; set; }
    }

    public class DomConfLinePost
    {
        public int? SRLNO { get; set; }
        public string INVNO { get; set; }
        public DateTime? PACK_DATE { get; set; }
        public string GRADE { get; set; }
        public string MARK { get; set; }
        public string CAT { get; set; }
        public string LOTNO { get; set; }
        public decimal? BAG_CHEST { get; set; }
        public decimal? CHESTSLF { get; set; }
        public decimal? CHESTSLT { get; set; }
        public decimal? NET_WT { get; set; }
        public decimal? TNET_WT { get; set; }
        public decimal? TGROSS_WT { get; set; }
        public decimal? ORDER_QTY { get; set; }
        public decimal? SAMPLE_QTY { get; set; }
        public decimal? OTHER_QTY { get; set; }
        public decimal? RATE { get; set; }
        public string WPCD { get; set; }
        public string PARTY_CODE { get; set; }
        public string SAUDA_DOCNO { get; set; }
        public DateTime? SAUDA_DOCDT { get; set; }
        public string BUYER_WH_CODE { get; set; }
        public string DUMMY_INVNO { get; set; }
        public string DUMMY_MARK { get; set; }
        public string DUMMY_GRADE { get; set; }
    }
}
