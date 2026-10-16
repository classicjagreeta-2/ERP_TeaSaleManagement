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
    // "Sales Invoice Entry" of the HO Sales package -- ported from VB6 trn_salesInvoice.frm, menu
    // "5. Sales Invoice Entry". Two menu rows open the same VB form with a different pSale_tag, so
    // there are two controllers (each its own AEDV rights row / record-lock screen) sharing this
    // base and ONE set of views in Views/TeaSaleInvoice (rendered by explicit path):
    //   * PrivateSaleInvoiceController -- "5.1 Private Sale" (pSale_tag "", API tag "P")
    //   * AuctionSaleInvoiceController -- "5.3 Auction Sale" (pSale_tag "4", API tag "4")
    // List and entry screen follow Birla Tea Sale of ERP_PacketSalesExport (the "TB" port of the
    // same form): the list's "New" row picks the Unit, the list is scoped to the Units the user is
    // linked to (USER_SCHEMA_LINK), pages as a chunked infinite scroll, and Add / Edit / Delete /
    // View + the back-date window come from AEDV. An invoice is identified by Unit + Tax Invoice
    // No + Date (T_SALESINVOICE has no header row). The API (api/TeaSaleInvoice) recomputes every
    // figure, writes the brokerage / tea board bills, posts the sales voucher and runs the stock
    // check in one transaction.
    [TeaSaleManagement.Utility.SessionRequired]
    public abstract class TeaSaleInvoiceControllerBase : Controller
    {
        protected const string Api = "/api/TeaSaleInvoice/";
        protected const string ViewDir = "~/Views/TeaSaleInvoice/";

        // AEDV.ForScreen key + record-lock screen (= the MVC controller name).
        protected abstract string Screen { get; }
        // API sale tag: "P" (private) or "4" (auction).
        protected abstract string Tag { get; }
        // Page title / captions.
        protected abstract string Caption { get; }

        private static string E(string s) => Uri.EscapeDataString(s ?? "");

        private AEDV Permission => AEDV.ForScreen((List<AEDV>)Session["User_AEDV"], Screen);

        private void SetScreenBag()
        {
            ViewBag.Ctl = Screen;
            ViewBag.Caption = Caption;
            ViewBag.Tag = Tag;
            ViewBag.IsAuctionScreen = Tag == "4";
        }

        // The Units the user is linked to (USER_SCHEMA_LINK) -- backs the list scope, the
        // "New" row's Unit picker and the Unit checks on open / save / delete.
        private async Task<List<UnitOption>> GetUnitsForUserAsync()
        {
            var r = await Services.GetAsync<List<UnitOption>>(Api + "GetUnitsForUser");
            return (r.IsSuccessStatusCode ? r.Data : null) ?? new List<UnitOption>();
        }

        // GET: <Screen>/Index
        public async Task<ActionResult> Index(string searchString, int? page = 1, int pageSize = 15, string sortBy = "", string sortDir = "", string unit = "")
        {
            SetScreenBag();
            ViewBag.Permission = Permission;
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
                $"{Api}GetByPage?tag={Tag}&unit={unitFilter}&search={E(searchString)}&page={pageNo}&pageSize={pageSize}&sortBy={E(sortBy)}&sortDir={E(sortDir)}");

            var value = response?.Data?["value"];
            var list = value?["results"]?.ToObject<List<TeaSaleInvoiceRow>>() ?? new List<TeaSaleInvoiceRow>();
            ViewBag.RowCount = value?["rowCount"]?.Value<int>() ?? 0;
            // Rows another user is editing are greyed out (see Utility/RecordLock.cs).
            ViewBag.Locks = await RecordLock.LocksAsync(Screen);

            if (isChunkRequest)
            {
                // A failed chunk must not leave a toast queued for the next full page load; an
                // empty response tells the list's scroll loader to stop.
                return PartialView(ViewDir + "_ListRows.cshtml", (response?.IsSuccessStatusCode ?? false) ? list : new List<TeaSaleInvoiceRow>());
            }

            if (!(response?.IsSuccessStatusCode ?? false))
            {
                TempData["toastrError"] = !string.IsNullOrEmpty(response?.Message)
                    ? response.Message
                    : $"Unable to load the {Caption} list (API returned {response?.StatusCode}).";
            }
            return View(ViewDir + "Index.cshtml", list);
        }

        // Tax Invoice Date picker's lower bound -- the later of the financial-year start and the
        // AEDV back-date allowance (Aday for a new invoice, Eday while editing). Save() is the
        // real gate; this only steers the date picker.
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

        // GET: <Screen>/InsertOrUpdate
        // New: `unit` comes from the list's "New" row and is locked here.
        // Edit/View: the invoice's key (unit, docno = Tax Invoice No, docdt = its date).
        public async Task<ActionResult> InsertOrUpdate(string unit = "", string docno = "", string docdt = "", bool view = false)
        {
            SetScreenBag();
            var permission = Permission;
            ViewBag.Permission = permission;

            bool isNew = string.IsNullOrEmpty(docno);

            // `view` only applies to an existing invoice and needs the View right.
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
                    TempData["toastrError"] = ctx.Message ?? "Unable to open a new invoice.";
                    return RedirectToAction("Index");
                }
                ViewBag.IsEdit = false;
                ViewBag.DocJson = ScriptJson.Serialize(new { head = ctx.Data, lines = new object[0], damages = new object[0], brokerage = new object[0] });
                return View(ViewDir + "InsertOrUpdate.cshtml");
            }

            var unitsTask2 = GetUnitsForUserAsync();
            var docTask = Services.GetAsync<JObject>($"{Api}GetByDocNo?tag={Tag}&unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}");
            await Task.WhenAll(unitsTask2, docTask);
            var r = docTask.Result;
            if (!r.IsSuccessStatusCode || r.Data == null)
            {
                TempData["toastrError"] = r.Message ?? "Record not found.";
                return RedirectToAction("Index");
            }

            // The invoice's own Unit must be one the user is linked to.
            var head = r.Data["head"];
            var docUnit = (string)head?["UNIT"];
            if (!UnitScope.IsAllowed(docUnit, unitsTask2.Result))
            {
                TempData["toastrError"] = $"You do not have permission for Unit {docUnit}.";
                return RedirectToAction("Index");
            }

            // Block opening Edit outright when the user has no Edit right, the invoice's own date
            // has fallen outside the Eday back-date window, or the API's edit guard refuses it
            // (E-Invoice generated / printed / purchase entry done) -- View bypasses this.
            if (!view)
            {
                var editErr = AEDV.CheckAddEdit(permission, false, (DateTime?)head?["DESP_DATE"] ?? DateTime.Today);
                if (editErr == null && !string.IsNullOrWhiteSpace((string)head?["EDIT_GUARD"]))
                    editErr = (string)head["EDIT_GUARD"];
                if (editErr != null)
                {
                    TempData["toastrError"] = editErr;
                    return RedirectToAction("Index");
                }

                // Edit lock: refused while another user has this invoice open for editing.
                var lockUnit = !string.IsNullOrEmpty(docUnit) ? docUnit : unit;
                var lockDate = (object)(DateTime?)head?["DESP_DATE"] ?? docdt;
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
            return View(ViewDir + "InsertOrUpdate.cshtml");
        }

        // POST: <Screen>/Save (JSON body). The API recomputes every figure, writes the brokerage /
        // tea board bills, posts the accounts voucher and checks the stock. dryRun=true runs it all
        // and rolls back.
        [HttpPost]
        public async Task<ActionResult> Save(bool dryRun = false)
        {
            TEA_SALE_INVOICE_POST model;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                    model = JsonConvert.DeserializeObject<TEA_SALE_INVOICE_POST>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                return JsonExact(new { success = false, message = "Invalid data: " + ex.Message });
            }
            if (model == null) return JsonExact(new { success = false, message = "Nothing to save." });
            model.TAG = Tag;

            // Server-side AEDV Add/Edit + back-date enforcement -- the date picker's `min` only
            // steers well-behaved clients; this is the actual gate. An edit is checked against
            // the invoice's saved date as well as the new one.
            bool isNew = model.IsNew ?? true;
            model.IsNew = isNew;
            var permission = Permission;
            var permErr = AEDV.CheckAddEdit(permission, isNew, model.DESP_DATE ?? DateTime.Today);
            if (permErr == null && !isNew && model.ORIG_DESP_DATE.HasValue)
                permErr = AEDV.CheckAddEdit(permission, false, model.ORIG_DESP_DATE.Value);
            if (permErr != null) return JsonExact(new { success = false, message = permErr });

            // The Unit (a NEW invoice is stamped with the one picked on the list; an edit keeps
            // the invoice's own) must be one the user is linked to.
            if (!UnitScope.IsAllowed(model.UNIT, await GetUnitsForUserAsync()))
                return JsonExact(new { success = false, message = $"You do not have permission for Unit {model.UNIT}." });

            // Edit lock: an edit may only be saved while no other user holds the invoice. The
            // lock is on the saved key (Unit + Tax Invoice No + the saved date).
            var lockDate = model.ORIG_DESP_DATE ?? model.DESP_DATE;
            if (!isNew)
            {
                var lockErr = await RecordLock.AcquireAsync(Screen, model.UNIT, model.DESP_NO, lockDate);
                if (lockErr != null) return JsonExact(new { success = false, message = lockErr });
            }

            var r = await Services.PostAsync<JObject>(Api + "SaveOrUpdate" + (dryRun ? "?dryRun=true" : ""), model);
            // The API answers HTTP 200 with success=false for the 50 lakh TCS question
            // (tcsPrompt) and after flagging the party (tcsApplied) -- neither is a save.
            bool saved = r.IsSuccessStatusCode && (r.Data?["success"]?.Value<bool>() ?? false);
            bool tcsPrompt = r.IsSuccessStatusCode && (r.Data?["tcsPrompt"]?.Value<bool>() ?? false);
            bool tcsApplied = r.IsSuccessStatusCode && (r.Data?["tcsApplied"]?.Value<bool>() ?? false);
            // A dry run rolls back -- the user is still editing, so the lock stays.
            if (saved && !isNew && !dryRun)
                await RecordLock.ReleaseAsync(Screen, model.UNIT, model.DESP_NO, lockDate);
            return JsonExact(new
            {
                success = saved,
                tcsPrompt,
                tcsApplied,
                message = saved ? "Saved successfully."
                        : r.IsSuccessStatusCode ? ((string)r.Data?["message"] ?? "Save failed.")
                        : (r.Message ?? "Save failed."),
                data = r.Data
            });
        }

        // POST: <Screen>/Delete
        [HttpPost]
        public async Task<ActionResult> Delete(string unit, string docno, string docdt)
        {
            var delErr = AEDV.CheckDelete(Permission, DateText.Parse(docdt));
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

            // Not while another user has the invoice open for editing.
            var lockErr = await RecordLock.AcquireAsync(Screen, unit, docno, docdt);
            if (lockErr != null)
            {
                TempData["toastrError"] = lockErr;
                return RedirectToAction("Index");
            }

            var r = await Services.PostAsync<JObject>(
                $"{Api}Delete?tag={Tag}&unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}", new { });
            if (!r.IsSuccessStatusCode)
                await RecordLock.ReleaseAsync(Screen, unit, docno, docdt);
            TempData[r.IsSuccessStatusCode ? "toastrSuccess" : "toastrError"] =
                r.IsSuccessStatusCode ? $"{docno} deleted." : (r.Message ?? "Delete failed.");
            return RedirectToAction("Index");
        }

        // =====================================================================
        // AJAX passthroughs (lookups search every displayed column)
        // =====================================================================
        protected ActionResult JsonExact(object data) =>
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
        // already holds a value -- that value as `value`, looked up as an exact code.
        private async Task<ActionResult> Picker(string path, string q, string value, int limit, int p, string extra = "")
        {
            var arg = string.IsNullOrWhiteSpace(q) && !string.IsNullOrWhiteSpace(value)
                ? "code=" + E(value.Trim())
                : "search=" + E((q ?? "").Trim());
            return PickerJson(await Services.GetAsync<JToken>($"{Api}{path}?{arg}{extra}"), limit, p);
        }

        // The list's "+" row / Lines button -- the invoice's lines in one lean call.
        // Nothing for a Unit the user is not linked to (the API itself checks no grants).
        [HttpGet] public async Task<ActionResult> GetRowLines(string unit = "", string docno = "", string docdt = "")
        {
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync())) return JsonExact(new { details = new object[0] });
            return await Lookup($"GetRowLines?tag={Tag}&unit={E(unit)}&docno={E(docno)}&docdt={E(docdt)}");
        }
        [HttpGet] public Task<ActionResult> GetRowDetail(string unit = "", string docno = "", string docdt = "") =>
            GetRowLines(unit, docno, docdt);

        [HttpGet] public Task<ActionResult> GetSaleTypes(string unit = "", string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetSaleTypes", q, value, limit, p, $"&tag={Tag}&unit={E(unit)}");
        [HttpGet] public Task<ActionResult> GetParty(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetParty", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetCustomer(string broker = "", string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetCustomer", q, value, limit, p, $"&broker={E(broker)}");
        [HttpGet] public Task<ActionResult> GetSalesCentre(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetSalesCentre", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetBroker(string unit = "", string saleType = "", string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetBroker", q, value, limit, p, $"&unit={E(unit)}&saleType={E(saleType)}");
        [HttpGet] public Task<ActionResult> GetTransporter(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetTransporter", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetDestination(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetDestination", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetBank(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetBank", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetMark(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetMark", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetCategory(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetCategory", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetGstCode(string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetGstCode", q, value, limit, p);
        [HttpGet] public Task<ActionResult> GetTdsHead(string unit = "", string q = "", int limit = 0, string fieldValue = "", string fieldText = "", string value = "", int p = 1) =>
            Picker("GetTdsHead", q, value, limit, p, $"&unit={E(unit)}");

        // Auction "Sale No" help (a composite pick -- sale / broker / mark -- shown in a bespoke popup).
        [HttpGet]
        public async Task<ActionResult> GetAuctionSales(string unit = "", string search = "")
        {
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync())) return JsonExact(new object[0]);
            return await Lookup($"GetAuctionSales?unit={E(unit)}&search={E(search)}");
        }

        // "Select Data" (composite, multi-row selection -- a bespoke popup, not an inputpicker):
        // the confirmations (private) / sold catalogue lots (auction) still to be billed; `unit`
        // must be one the user is linked to.
        [HttpGet]
        public async Task<ActionResult> GetPending(string unit = "", string saleType = "", string saleCentre = "", string billTo = "", string shipTo = "",
                                                   string cust = "", string broker = "", string saleNo = "", string docdt = "", string mark = "",
                                                   string cat = "", string mode = "", string basedOn = "")
        {
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync())) return JsonExact(new object[0]);
            return await Lookup($"GetPending?tag={Tag}&unit={E(unit)}&saleType={E(saleType)}&saleCentre={E(saleCentre)}&billTo={E(billTo)}&shipTo={E(shipTo)}" +
                                $"&cust={E(cust)}&broker={E(broker)}&saleNo={E(saleNo)}&docdt={E(docdt)}&mark={E(mark)}&cat={E(cat)}&mode={E(mode)}&basedOn={E(basedOn)}");
        }

        [HttpGet]
        public async Task<ActionResult> GetPendingHint(string unit = "", string saleType = "", string saleCentre = "", string billTo = "",
                                                       string broker = "", string saleNo = "")
        {
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync())) return JsonExact(new { message = "No unit is available to you." });
            return await Lookup($"GetPendingHint?tag={Tag}&saleType={E(saleType)}&saleCentre={E(saleCentre)}&billTo={E(billTo)}" +
                                $"&broker={E(broker)}&saleNo={E(saleNo)}");
        }

        [HttpGet] public async Task<ActionResult> GetDamage(string unit = "", string invno = "", string mark = "")
        {
            if (!UnitScope.IsAllowed(unit, await GetUnitsForUserAsync())) return JsonExact(new object[0]);
            return await Lookup($"GetDamage?invno={E(invno)}&mark={E(mark)}");
        }
        [HttpGet] public Task<ActionResult> GetRates(string unit = "", string saleCentre = "", string billTo = "", string shipTo = "", string docdt = "", string grades = "", string gstCodes = "") =>
            Lookup($"GetRates?unit={E(unit)}&saleCentre={E(saleCentre)}&billTo={E(billTo)}&shipTo={E(shipTo)}&docdt={E(docdt)}&grades={E(grades)}&gstCodes={E(gstCodes)}");
    }

    public class TeaSaleInvoiceRow
    {
        public string UNIT { get; set; }
        public string DESP_NO { get; set; }
        public DateTime? DESP_DATE { get; set; }
        public string SALE_TYPE { get; set; }
        public string SALE_TYPE_NAME { get; set; }
        public string BILL_TO_PARTY { get; set; }
        public string BILL_TO_NAME { get; set; }
        public string PCODE { get; set; }
        public string SHIP_TO_NAME { get; set; }
        public string SALE_CENTRE_NAME { get; set; }
        public string BROK_CODE { get; set; }
        public string BROKER_NAME { get; set; }
        public string SALE_NO { get; set; }
        public string JV_DOCNO { get; set; }
        public string IRN { get; set; }
        public string PRNT_TAG { get; set; }
        public decimal? BAGS { get; set; }
        public decimal? BILL_QTY { get; set; }
        public decimal? TOTAL_AMT { get; set; }
    }

    // What the entry screen posts to Save (passed on to api/TeaSaleInvoice/SaveOrUpdate).
    public class TEA_SALE_INVOICE_POST
    {
        public string TAG { get; set; }
        public string UNIT { get; set; }
        public bool? IsNew { get; set; }
        public string DESP_NO { get; set; }
        public DateTime? DESP_DATE { get; set; }
        public DateTime? ORIG_DESP_DATE { get; set; }
        public string SALE_TYPE { get; set; }
        public string SALE_CENTRE { get; set; }
        public string PCODE { get; set; }
        public string BILL_TO_PARTY { get; set; }
        public string PARTY_CODE { get; set; }
        public string BROK_CODE { get; set; }
        public decimal BROKERAGE_RATE { get; set; }
        public string SALE_NO { get; set; }
        public DateTime? SALE_DATE { get; set; }
        public string CONS_NO { get; set; }
        public DateTime? CONS_DT { get; set; }
        public string TPT { get; set; }
        public string TNO { get; set; }
        public string WAY_BILL { get; set; }
        public string DESP_BY { get; set; }
        public string DEST_TO { get; set; }
        public string DEST_FROM { get; set; }
        public DateTime? ISSUE_DT { get; set; }
        public string ISSUE_TM { get; set; }
        public DateTime? REMOVAL_DT { get; set; }
        public string REMOVAL_TM { get; set; }
        public DateTime? PROMPT_DATE { get; set; }
        public string ORDNO { get; set; }
        public DateTime? ORDDT { get; set; }
        public decimal TOT_GSTAMT_M { get; set; }
        public string BANK_ACODE { get; set; }
        public string TCS_ANSWER { get; set; }
        public string TDS_CODE { get; set; }
        public List<TEA_SALE_INVOICE_POST_LINE> LINES { get; set; }
        public List<TEA_SALE_BROKERAGE_POST> BROKERAGE { get; set; }
    }

    public class TEA_SALE_INVOICE_POST_LINE
    {
        public string INVNO { get; set; }
        public DateTime? PACK_DATE { get; set; }
        public string GRADE { get; set; }
        public string MARK { get; set; }
        public string SEASON { get; set; }
        public string CAT { get; set; }
        public decimal BAG_CHEST { get; set; }
        public decimal NET_WT { get; set; }
        public decimal TNET_WT { get; set; }
        public decimal TGROSS_WT { get; set; }
        public decimal INSURANCE { get; set; }
        public decimal EXTRA_BILLED_WT { get; set; }
        public decimal BILL_QTY { get; set; }
        public decimal SAMPLE_QTY { get; set; }
        public decimal OTHER_QTY { get; set; }
        public decimal RATE { get; set; }
        public decimal DISC_PER { get; set; }
        public string ORDER_DOCNO { get; set; }
        public DateTime? ORDER_DOCDT { get; set; }
        public string DAMAGE_CHECK { get; set; }
        public string AWR_DOCNO { get; set; }
        public DateTime? AWR_DATE { get; set; }
        public string WAREHOUSE { get; set; }
        public int SALE_SRLNO { get; set; }
        public string SAUDA_DOCNO { get; set; }
        public DateTime? SAUDA_DOCDT { get; set; }
        public string BUYER_WH_CODE { get; set; }
        public List<TEA_SALE_INVOICE_POST_DAMAGE> DAMAGES { get; set; }
    }

    public class TEA_SALE_INVOICE_POST_DAMAGE
    {
        public decimal BAG_CHEST { get; set; }
        public bool WS_QTY { get; set; }
        public bool BR_QTY { get; set; }
        public bool TR_QTY { get; set; }
        public bool SR_QTY { get; set; }
        public bool OS_QTY { get; set; }
        public decimal SR_KGS { get; set; }
        public string REMARK { get; set; }
        public bool TAG_OK { get; set; }
        public string DOCNO { get; set; }
        public DateTime? AWR_DATE { get; set; }
    }

    // One row of the "Brokerage && Tea Board Bill Details" frame (T_BROKERAGEBILL).
    public class TEA_SALE_BROKERAGE_POST
    {
        public string DOC_TYPE { get; set; }
        public string DOCNO { get; set; }
        public string INVOICENO { get; set; }
        public DateTime? INVOICEDT { get; set; }
        public string SAC_CODE { get; set; }
        public string SAC_DESCN { get; set; }
        public decimal TNET_WT { get; set; }
        public decimal INV_AMOUNT { get; set; }
        public decimal RATE { get; set; }
        public decimal LOT_MONEY { get; set; }
        public decimal SERV_CHG { get; set; }
        public decimal INSP_CHG { get; set; }
        public decimal RE_INSP_CHG { get; set; }
        public decimal SELLER_CHG { get; set; }
        public decimal TAXABLE_AMT { get; set; }
        public string GST_CODE { get; set; }
        public decimal TDS_AMT { get; set; }
        public decimal TDS_PER { get; set; }
        public decimal TCS_AMT { get; set; }
    }
}
