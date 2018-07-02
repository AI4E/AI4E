using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace AI4E.Modularity.Hosting.Sample.Controllers
{
    public sealed class ModulesController : Controller
    {
        private readonly IMessageDispatcher _messageDispatcher;

        public ModulesController(IMessageDispatcher messageDispatcher)
        {
            _messageDispatcher = messageDispatcher;
        }


        public async Task<IActionResult> Index()
        {
            return null;
        }
    }
}
