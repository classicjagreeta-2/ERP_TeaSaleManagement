using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    // HS menu "Masters" -- the factory / Bought Leaf (Out-Grower) masters: Vehicle Type, Vehicle, Transporter
    // (VB mast_vehicleTyp / mast_vehicle / mast_transp, Fact_2) and the new Clerk, Route, Centre, Weighment
    // Point, Driver and Helper/Loader masters. One API controller (api/FactoryMaster, which holds each master's
    // fields and VB rules and re-validates everything) and one set of views (Views/FactoryMaster, drawn from the
    // API's GetMeta); each screen is a thin subclass so it keeps its own menu row, AEDV row and record lock.
    //
    // Masters have no Doc Date, so AEDV is the rights only (A / E / D / V), no back-date window. A master kept
    // by unit (Vehicle, Clerk, Route, Centre, Weighment Point) picks its Unit on the list's "New" row (locked on
    // the entry screen) and its list is scoped to the user's USER_SCHEMA_LINK units; the others have no unit.
    [SessionRequired]
    public abstract class FactoryMasterControllerBase : Controller
    {
        protected const string Api = "/api/FactoryMaster/";
        protected const string ViewDir = "~/Views/FactoryMaster/";

        // AEDV.ForScreen key + record-lock screen (= the MVC controller name).
        protected abstract string Screen { get; }
        // api/FactoryMaster master key.
        protected abstract string Master { get; }

        private static string E(string s) => Uri.EscapeDataString(s ?? "");

        private AEDV Permission => AEDV.ForScreen((List<AEDV>)Session["User_AEDV"], Screen);

        private async Task<List<UnitOption>> GetUnitsForUserAsync()
        {
            var r = await Services.GetAsync<List<UnitOption>>(Api + "GetUnitsForUser");
            return (r.IsSuccessStatusCode ? r.Data : null) ?? new List<UnitOption>();
        }

        private async Task<JObject> GetMetaAsync()
        {
            var r = await Services.GetAsync<JObject>($"{Api}GetMeta?master={E(Master)}");
            return r.IsSuccessStatusCode ? r.Data : null;
        }

        private static bool ByUnit(JObject meta) => meta?["BY_UNIT"]?.Value<bool>() ?? false;

        private void SetScreenBag(JObject meta)
        {
            ViewBag.Ctl = Screen;
            ViewBag.Meta = meta;
            ViewBag.MetaJson = meta == null ? "{}" : ScriptJson.Serialize(meta);
            ViewBag.ScreenName = (string)meta?["TITLE"] ?? Screen;
            ViewBag.ByUnit = ByUnit(meta);
        }

        // GET: <Screen>  -- list (chunked infinite scroll, see Scripts/list-infinite-scroll.js).
        public async Task<ActionResult> Index(string searchString, int? page = 1, int pageSize = 15, string sortBy = "", string sortDir = "", string unit = "")
        {
            var metaTask = GetMetaAsync();
            var unitsTask = GetUnitsForUserAsync();
            await Task.WhenAll(metaTask, unitsTask);
            var meta = metaTask.Result;
            SetScreenBag(meta);
            ViewBag.Permission = Permission;
            ViewBag.CurrentFilter = searchString;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDir = sortDir;
            ViewBag.PageSize = pageSize;
            var userUnits = unitsTask.Result;
            ViewBag.UnitList = userUnits;

            var isChunkRequest = Request.IsAjaxRequest();
            var pageNo = isChunkRequest ? Math.Max(page ?? 1, 1) : 1;
            ViewBag.Page = pageNo;
            ViewBag.RowOffset = (pageNo - 1) * pageSize;

            var unitFilter = ByUnit(meta) ? E(UnitScope.ListFilter(unit, userUnits)) : "";
            var response = await Services.GetAsync<JObject>(
                $"{Api}GetByPage?master={E(Master)}&unit={unitFilter}&search={E(searchString)}&page={pageNo}&pageSize={pageSize}&sortBy={E(sortBy)}&sortDir={E(sortDir)}");

            var value = response?.Data?["value"];
            var list = value?["results"]?.ToObject<List<JObject>>() ?? new List<JObject>();
            ViewBag.RowCount = value?["rowCount"]?.Value<int>() ?? 0;
            ViewBag.Locks = await RecordLock.LocksAsync(Screen);

            if (isChunkRequest)
                return PartialView(ViewDir + "_ListRows.cshtml", (response?.IsSuccessStatusCode ?? false) ? list : new List<JObject>());

            if (meta == null)
                TempData["toastrError"] = "Unable to load the screen definition (API api/FactoryMaster/GetMeta).";
            else if (!(response?.IsSuccessStatusCode ?? false))
                TempData["toastrError"] = !string.IsNullOrEmpty(response?.Message) ? response.Message
                    : $"Unable to load the {ViewBag.ScreenName} list (API returned {response?.StatusCode}).";
            return View(ViewDir + "Index.cshtml", list);
        }

        // GET: <Screen>/InsertOrUpdate -- New: `unit` from the list's New row (masters kept by unit);
        // Edit / View: the record's code (+ unit).
        public async Task<ActionResult> InsertOrUpdate(string unit = "", string code = "", bool view = false)
        {
            var meta = await GetMetaAsync();
            if (meta == null)
            {
                TempData["toastrError"] = "Unable to load the screen definition.";
                return RedirectToAction("Index");
            }
            SetScreenBag(meta);
            var permission = Permission;
            ViewBag.Permission = permission;
            bool byUnit = ByUnit(meta);
            bool isNew = string.IsNullOrEmpty(code);

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
            if (!isNew && !view && !(permission?.Edit ?? false))
            {
                TempData["toastrError"] = "You do not have permission to edit this entry.";
                return RedirectToAction("Index");
            }

            var units = byUnit ? await GetUnitsForUserAsync() : new List<UnitOption>();
            if (byUnit && !UnitScope.IsAllowed(unit, units))
            {
                TempData["toastrError"] = string.IsNullOrEmpty(unit) ? "Select the Unit from the New row first." : $"You do not have permission for Unit {unit}.";
                return RedirectToAction("Index");
            }

            var r = isNew
                ? await Services.GetAsync<JObject>($"{Api}GetNewContext?master={E(Master)}&unit={E(unit)}")
                : await Services.GetAsync<JObject>($"{Api}GetByCode?master={E(Master)}&unit={E(unit)}&code={E(code)}");
            if (!r.IsSuccessStatusCode || r.Data == null)
            {
                TempData["toastrError"] = r.Message ?? (isNew ? "Unable to open a new entry." : "Record not found.");
                return RedirectToAction("Index");
            }

            if (!isNew && !view)
            {
                // Edit lock: refused while another user has this record open for editing.
                var lockErr = await RecordLock.AcquireAsync(Screen, byUnit ? unit : "", code);
                if (lockErr != null)
                {
                    TempData["toastrError"] = lockErr;
                    return RedirectToAction("Index");
                }
                ViewBag.RecordLock = RecordLock.ClientConfig(Screen, byUnit ? unit : "", code);
            }

            ViewBag.IsEdit = !isNew;
            ViewBag.DocJson = ScriptJson.Serialize(r.Data);
            return View(ViewDir + "InsertOrUpdate.cshtml");
        }

        // POST: <Screen>/Save (JSON body). The API re-validates everything.
        [HttpPost]
        public async Task<ActionResult> Save()
        {
            FactoryMasterData model;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                    model = JsonConvert.DeserializeObject<FactoryMasterData>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                return JsonExact(new { success = false, message = "Invalid data: " + ex.Message });
            }
            if (model == null) return JsonExact(new { success = false, message = "Nothing to save." });
            model.MASTER = Master;

            var permission = Permission;
            bool isNew = model.IsNew ?? string.IsNullOrWhiteSpace(model.CODE);
            if (isNew && !(permission?.Add ?? false)) return JsonExact(new { success = false, message = "You do not have permission to add a new entry." });
            if (!isNew && !(permission?.Edit ?? false)) return JsonExact(new { success = false, message = "You do not have permission to edit this entry." });
            if (!isNew && string.IsNullOrWhiteSpace(model.CODE)) return JsonExact(new { success = false, message = "Code is required." });

            var meta = await GetMetaAsync();
            bool byUnit = ByUnit(meta);
            if (byUnit && !UnitScope.IsAllowed(model.UNIT, await GetUnitsForUserAsync()))
                return JsonExact(new { success = false, message = $"You do not have permission for Unit {model.UNIT}." });
            if (!byUnit) model.UNIT = null;

            if (!isNew)
            {
                var lockErr = await RecordLock.AcquireAsync(Screen, model.UNIT ?? "", model.CODE);
                if (lockErr != null) return JsonExact(new { success = false, message = lockErr });
            }

            var r = await Services.PostAsync<JObject>(Api + "SaveOrUpdate", model);
            if (r.IsSuccessStatusCode && !isNew)
                await RecordLock.ReleaseAsync(Screen, model.UNIT ?? "", model.CODE);
            return JsonExact(new
            {
                success = r.IsSuccessStatusCode,
                message = r.IsSuccessStatusCode ? ((string)r.Data?["message"] ?? "Saved successfully.") : (r.Message ?? "Save failed."),
                data = r.Data
            });
        }

        // POST: <Screen>/Delete
        [HttpPost]
        public async Task<ActionResult> Delete(string unit, string code)
        {
            if (!(Permission?.Delete ?? false))
            {
                TempData["toastrError"] = "You do not have permission to delete this entry.";
                return RedirectToAction("Index");
            }
            var meta = await GetMetaAsync();
            bool byUnit = ByUnit(meta);
            if (byUnit && !UnitScope.IsAllowed(unit, await GetUnitsForUserAsync()))
            {
                TempData["toastrError"] = $"You do not have permission for Unit {unit}.";
                return RedirectToAction("Index");
            }
            if (!byUnit) unit = "";

            var lockErr = await RecordLock.AcquireAsync(Screen, unit, code);
            if (lockErr != null)
            {
                TempData["toastrError"] = lockErr;
                return RedirectToAction("Index");
            }

            var r = await Services.PostAsync<JObject>($"{Api}Delete?master={E(Master)}&unit={E(unit)}&code={E(code)}", new { });
            if (!r.IsSuccessStatusCode)
                await RecordLock.ReleaseAsync(Screen, unit, code);
            TempData[r.IsSuccessStatusCode ? "toastrSuccess" : "toastrError"] =
                r.IsSuccessStatusCode ? $"{code} deleted." : (r.Message ?? "Delete failed.");
            return RedirectToAction("Index");
        }

        // =====================================================================
        // AJAX: lookup pickers (jquery.inputpicker: q / limit / p -> { data, count })
        // =====================================================================
        private ActionResult JsonExact(object data) =>
            Content(JsonConvert.SerializeObject(data), "application/json");

        // The API matches `q` against every column the dropdown shows (code, name, info); a loaded record's
        // value is looked up as an exact code so its own row shows.
        [HttpGet]
        public async Task<ActionResult> GetLookup(string field = "", string unit = "", string q = "", int limit = 0, string fieldValue = "",
                                                  string fieldText = "", string value = "", int p = 1)
        {
            // Unit-kept lookups (Route for Centre / Weighment Point) list only a permitted unit's rows.
            if (!string.IsNullOrWhiteSpace(unit) && !UnitScope.IsAllowed(unit, await GetUnitsForUserAsync()))
                return JsonExact(new { data = new object[0], count = 0 });
            var arg =string.IsNullOrWhiteSpace(q) && !string.IsNullOrWhiteSpace(value)
                ? "code=" + E(value.Trim())
                : "search=" + E((q ?? "").Trim());
            var r = await Services.GetAsync<JToken>($"{Api}GetLookup?master={E(Master)}&field={E(field)}&unit={E(unit)}&{arg}");
            if (!r.IsSuccessStatusCode)
            {
                bool isAuthFailure = string.Equals(r.StatusCode, HttpStatusCode.Unauthorized.ToString(), StringComparison.OrdinalIgnoreCase);
                Response.StatusCode = isAuthFailure ? 440 : 500;
                Response.TrySkipIisCustomErrors = true;
                return JsonExact(new
                {
                    sessionExpired = isAuthFailure,
                    message = isAuthFailure ? "Your session has expired. Please log in again." : (r.Message ?? "Unable to load the list.")
                });
            }
            var arr = r.Data as JArray ?? new JArray();
            var take = limit > 0 ? limit : 10;
            return JsonExact(new { data = arr.Skip((Math.Max(p, 1) - 1) * take).Take(take), count = arr.Count });
        }
    }

    // Body posted to Save, forwarded to POST api/FactoryMaster/SaveOrUpdate.
    public class FactoryMasterData
    {
        public string MASTER { get; set; }
        public string UNIT { get; set; }
        public string CODE { get; set; }
        public string NAME { get; set; }
        public bool? IsNew { get; set; }
        public Dictionary<string, string> VALUES { get; set; } = new Dictionary<string, string>();
    }
}
