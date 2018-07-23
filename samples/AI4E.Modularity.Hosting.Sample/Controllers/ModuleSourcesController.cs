using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Modularity.Hosting.Sample.Api;
using AI4E.Modularity.Hosting.Sample.Models;
using Microsoft.AspNetCore.Mvc;

namespace AI4E.Modularity.Hosting.Sample.Controllers
{
    public sealed class ModuleSourcesController : Controller
    {
        private readonly IMessageDispatcher _messageDispatcher;

        public ModuleSourcesController(IMessageDispatcher messageDispatcher)
        {
            _messageDispatcher = messageDispatcher;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var queryResult = await _messageDispatcher.QueryAsync<IEnumerable<ModuleSourceListModel>>();

            if (queryResult.IsSuccessWithResult<IEnumerable<ModuleSourceListModel>>(out var model))
            {
                return View(model);
            }

            return GetActionResult(queryResult);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var queryResult = await _messageDispatcher.QueryByIdAsync<ModuleSourceModel>(id);

            if (queryResult.IsSuccessWithResult<ModuleSourceModel>(out var model))
            {
                return View(model);
            }

            return GetActionResult(queryResult);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new ModuleSourceCreateModel();

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(ModuleSourceCreateModel model)
        {
            var id = Guid.NewGuid();
            var command = new ModuleSourceAddCommand(id, model.Name, model.Location);
            var commandResult = await _messageDispatcher.DispatchAsync(command);

            if (commandResult.IsSuccess)
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            return GetActionResult(commandResult, model);
        }

        [HttpGet]
        public async Task<IActionResult> Rename(Guid id)
        {
            var queryResult = await _messageDispatcher.QueryByIdAsync<ModuleSourceRenameModel>(id);

            if (queryResult.IsSuccessWithResult<ModuleSourceRenameModel>(out var model))
            {
                return View(model);
            }

            return GetActionResult(queryResult);
        }

        [HttpPost]
        public async Task<IActionResult> Rename(ModuleSourceRenameModel model)
        {
            var command = new ModuleSourceRenameCommand(model.Id, model.ConcurrencyToken, model.Name);
            var commandResult = await _messageDispatcher.DispatchAsync(command);

            if (commandResult.IsSuccess)
            {
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }

            return GetActionResult(commandResult, model);
        }

        [HttpGet]
        public async Task<IActionResult> UpdateLocation(Guid id)
        {
            var queryResult = await _messageDispatcher.QueryByIdAsync<ModuleSourceUpdateLocationModel>(id);

            if (queryResult.IsSuccessWithResult<ModuleSourceUpdateLocationModel>(out var model))
            {
                return View(model);
            }

            return GetActionResult(queryResult);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateLocation(ModuleSourceUpdateLocationModel model)
        {
            var command = new ModuleSourceUpdateLocationCommand(model.Id, model.ConcurrencyToken, model.Location);
            var commandResult = await _messageDispatcher.DispatchAsync(command);

            if (commandResult.IsSuccess)
            {
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }

            return GetActionResult(commandResult, model);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(Guid id)
        {
            var queryResult = await _messageDispatcher.QueryByIdAsync<ModuleSourceDeleteModel>(id);

            if (queryResult.IsSuccessWithResult<ModuleSourceDeleteModel>(out var model))
            {
                return View(model);
            }

            return GetActionResult(queryResult);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(ModuleSourceDeleteModel model)
        {
            var command = new ModuleSourceRemoveCommand(model.Id, model.ConcurrencyToken);
            var commandResult = await _messageDispatcher.DispatchAsync(command);

            if (commandResult.IsSuccess)
            {
                return RedirectToAction(nameof(Index));
            }

            return GetActionResult(commandResult, model);
        }

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
    }
}
