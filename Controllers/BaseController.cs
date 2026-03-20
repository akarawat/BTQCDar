using BTQCDar.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BTQCDar.Controllers
{
    public class BaseController : Controller
    {
        protected UserSessionModel? GetSession()
        {
            var json = HttpContext.Session.GetString("UserSession");
            if (string.IsNullOrEmpty(json)) return null;
            return JsonSerializer.Deserialize<UserSessionModel>(json);
        }

        protected void SaveSession(UserSessionModel model)
        {
            HttpContext.Session.SetString("UserSession", JsonSerializer.Serialize(model));
        }

        /// <summary>
        /// Checks session. Returns redirect action if not logged in,
        /// otherwise sets session via out-param and returns null.
        /// </summary>
        protected IActionResult? RequireLogin(out UserSessionModel session)
        {
            var s = GetSession();
            if (s == null || string.IsNullOrEmpty(s.SamAcc))
            {
                session = new UserSessionModel();
                return RedirectToAction("Index", "Dashboards");
            }
            session = s;
            return null;
        }
    }
}
