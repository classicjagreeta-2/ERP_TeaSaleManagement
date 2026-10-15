using System.Collections.Generic;
using System.Web.Mvc;
using static TeaSaleManagement.Helpers;

namespace TeaSaleManagement.Utility
{
    // The user's rights (Session["User_AEDV"]) and API token (Session["UserInfo"].getToken) live in
    // the in-process session, which can be lost while the Forms login cookie is still valid -- idle
    // past <sessionState timeout>, or IIS / IIS Express restarting the site (e.g. after a rebuild).
    // Without this filter every AEDV check then read "no rights" and Save said "You do not have
    // permission to add a new entry." although the user had the right. Instead:
    //   * AJAX / POST -> HTTP 440 { success:false, sessionExpired:true, message } (the screens'
    //     failMsg shows it and reloads the page);
    //   * a page request -> back to the login page.
    // Put it on every screen controller of this module (Controllers\HS).
    public class SessionRequiredAttribute : ActionFilterAttribute
    {
        public const string Message = "Your session has expired. Please log in again.";

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var http = filterContext.HttpContext;
            var session = http.Session;
            var user = session?["UserInfo"] as UserInfo;
            bool lost = session == null
                        || !(session["User_AEDV"] is List<AEDV>)
                        || user == null || string.IsNullOrEmpty(user.getToken);
            if (lost)
            {
                var req = http.Request;
                if (req.IsAjaxRequest() || req.HttpMethod == "POST" || (req.AcceptTypes != null && System.Array.IndexOf(req.AcceptTypes, "application/json") >= 0))
                {
                    http.Response.StatusCode = 440;
                    http.Response.TrySkipIisCustomErrors = true;
                    filterContext.Result = new JsonResult
                    {
                        Data = new { success = false, sessionExpired = true, message = Message },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
                else
                {
                    filterContext.Result = new RedirectToRouteResult(
                        new System.Web.Routing.RouteValueDictionary(new { controller = "Auth", action = "Login", area = "" }));
                }
                return;
            }
            base.OnActionExecuting(filterContext);
        }
    }
}
