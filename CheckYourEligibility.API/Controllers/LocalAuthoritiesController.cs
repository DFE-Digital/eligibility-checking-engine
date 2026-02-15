using Microsoft.AspNetCore.Mvc;

namespace CheckYourEligibility.API.Controllers
{
    public class LocalAuthoritiesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
