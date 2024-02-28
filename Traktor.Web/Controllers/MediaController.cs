using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Traktor.Web.Controllers
{
    public class MediaController : Controller
    {
        // GET: MediaController
        public ActionResult Index()
        {
            return View();
        }
    }
}
