using Microsoft.AspNetCore.Mvc;

namespace AI4E.Modularity.Hosting.Sample.Controllers
{
    public sealed class DefaultController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
