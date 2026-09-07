using BusinessLogic.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization; // Yeh namespace lazmi add karein
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;

public class BaseController : Controller
{
    protected readonly ISessionHelper _sessions;

    public BaseController(ISessionHelper session)
    {
        _sessions = session;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // 🔥 LINE: Check karein ki kya Action ya Controller par [AllowAnonymous] laga hai?
        bool hasAllowAnonymous = context.ActionDescriptor.EndpointMetadata
            .Any(em => em.GetType() == typeof(AllowAnonymousAttribute));

        // Agar [AllowAnonymous] laga hai, to session check SKIP kar dein
        if (hasAllowAnonymous)
        {
            base.OnActionExecuting(context);
            return;
        }

        // Baki sab ke liye purana session check logic
        if (_sessions.LoginId == 0)
        {
            bool isAjaxRequest = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (isAjaxRequest)
            {
                context.Result = new StatusCodeResult(401);
            }
            else
            {
                context.Result = new RedirectToActionResult("Login", "Home", null);
            }
            return;
        }

        base.OnActionExecuting(context);
    }
}