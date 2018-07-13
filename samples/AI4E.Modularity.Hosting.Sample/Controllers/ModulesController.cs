using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AI4E.Modularity.Hosting.Sample.Api;
using AI4E.Modularity.Hosting.Sample.Models;
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

        [HttpGet]
        public IActionResult Index()
        {
            return View(new ModulesSearchModel());
        }

        [HttpPost]
        public async Task<IActionResult> Index(ModulesSearchModel model)
        {
            var query = new ModuleSearchQuery(model.SearchPhrase, model.IncludePreReleases);
            var queryResult = await _messageDispatcher.DispatchAsync(query);

            if (queryResult.IsSuccessWithResult<IEnumerable<ModuleListModel>>(out var searchResult))
            {
                model.SearchResult = new List<ModuleListModel>(searchResult);
                return View(model);
            }

            return GetActionResult(queryResult);
        }

        [HttpGet]
        public async Task<IActionResult> Installed()
        {
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> UpdatesAvailable()
        {
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> Install(string module,
                                                 [FromQuery(Name = "module-version")] string moduleVersion = null,
                                                 [FromQuery(Name = "module-source")] Guid? moduleSource = null)
        {
            return null;
        }

        [HttpPost]
        public async Task<IActionResult> Install(ModuleInstallModel model)
        {
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> Uninstall(string module)
        {
            return null;
        }

        [HttpPost]
        public async Task<IActionResult> Uninstall(ModuleUninstallModel model)
        {
            return null;
        }

        #region TODO: Duplicates

        private IActionResult GetActionResult<TModel>(IDispatchResult dispatchResult, TModel model)
        {
            if (dispatchResult.IsValidationFailed(out var validationResults))
            {
                AddModelError(validationResults);
                return View(model);
            }

            return GetActionResult(dispatchResult);
        }

        private IActionResult GetActionResult(IDispatchResult dispatchResult)
        {
            if (dispatchResult.IsNotFound())
            {
                return NotFound();
            }

            if (dispatchResult.IsNotAuthenticated())
            {
                return StatusCode(401); // TODO: Rediect to authentication page
            }

            if (dispatchResult.IsNotAuthorized())
            {
                return Forbid();
            }

            return StatusCode(500);
        }

        private void AddModelError(IEnumerable<ValidationResult> validationResults)
        {
            foreach (var validationResult in validationResults)
            {
                ModelState.AddModelError(validationResult.Member, validationResult.Message);
            }
        }

        #endregion
    }
}
