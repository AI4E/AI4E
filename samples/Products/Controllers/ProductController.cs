using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E;
using Microsoft.AspNetCore.Mvc;
using Products.Api;
using Products.Models;

namespace Products.Controllers
{
    public class ProductController : Controller
    {
        private readonly IMessageDispatcher _messageDispatcher;

        public ProductController(IMessageDispatcher messageDispatcher)
        {
            if (messageDispatcher == null)
            {
                throw new ArgumentNullException(nameof(messageDispatcher));
            }

            _messageDispatcher = messageDispatcher;
        }

        [HttpGet]
        public async Task<IActionResult> Index(Guid id)
        {
            var dispatchResult = await _messageDispatcher.QueryByIdAsync<ProductModel>(id);
            if (dispatchResult.IsSuccessWithResult<ProductModel>(out var model))
            {
                return View(model);
            }
            return NotFound();
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new ProductCreateModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProductCreateModel model)
        {
            var command = new ProductCreateCommand(Guid.NewGuid(), default, model.Name);
            var commandResult = await _messageDispatcher.DispatchAsync(command);
            if (commandResult.IsSuccess)
            {
                return RedirectToAction(nameof(List));
            }
            if (commandResult.IsValidationFailed(out var validationResults))
            {
                foreach (var validationResult in validationResults)
                {
                    ModelState.AddModelError(validationResult.Member, validationResult.Message);
                }

                return View(model);
            }
            return StatusCode(500);
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var dispatchResult = await _messageDispatcher.QueryAsync<IEnumerable<ProductModel>>();
            if (dispatchResult.IsSuccessWithResult<IEnumerable<ProductModel>>(out var model))
            {
                return View(model);
            }

            return StatusCode(500);
        }

        [HttpGet]
        public async Task<IActionResult> Rename(Guid id)
        {
            var dispatchResult = await _messageDispatcher.QueryByIdAsync<ProductRenameModel>(id);
            if (dispatchResult.IsSuccessWithResult<ProductRenameModel>(out var model))
            {
                return View(model);
            }

            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> Rename(ProductRenameModel model)
        {
            var command = new ProductRenameCommand(model.Id, model.ConcurrencyToken, model.Name);
            var commandResult = await _messageDispatcher.DispatchAsync(command);
            if (commandResult.IsSuccess)
            {
                return RedirectToAction(nameof(List));
            }

            if (commandResult.IsValidationFailed(out var validationResults))
            {
                foreach (var validationResult in validationResults)
                {
                    ModelState.AddModelError(validationResult.Member, validationResult.Message);
                }

                return View(model);
            }

            return StatusCode(500);
        }

        [HttpGet]
        public async Task<IActionResult> ChangePrice(Guid id)
        {
            var dispatchResult = await _messageDispatcher.QueryByIdAsync<ProductChangePriceModel>(id);
            if (dispatchResult.IsSuccessWithResult<ProductChangePriceModel>(out var model))
            {
                return View(model);
            }

            return StatusCode(500);
        }

        [HttpPost]
        public async Task<IActionResult> ChangePrice(ProductChangePriceModel model)
        {
            var command = new ProductChangePriceCommand(model.Id, model.ConcurrencyToken, model.Price);
            var commandResult = await _messageDispatcher.DispatchAsync(command);
            if (commandResult.IsSuccess)
            {
                return RedirectToAction(nameof(List));
            }

            if (commandResult.IsValidationFailed(out var validationResults))
            {
                foreach (var validationResult in validationResults)
                {
                    ModelState.AddModelError(validationResult.Member, validationResult.Message);
                }

                return View(model);
            }
            return StatusCode(500);
        }

        //[HttpPost]
        //public async Task<IActionResult> Delete(ProductDeleteModel model)
        //{
        //    var command = new ProductDeleteCommand(model.Id, model.ConcurrencyToken);
        //    var commandResult = await _messageDispatcher.DispatchAsync(command);
        //    if(commandResult.IsSuccess)
        //    {
        //        return RedirectToAction(nameof(List));
        //    }

        //    if (commandResult.IsValidationFailed(out var validationResults))
        //    {
        //        foreach (var validationResult in validationResults)
        //        {
        //            ModelState.AddModelError(validationResult.Member, validationResult.Message);
        //        }

        //        return View(model);
        //    }
        //    return StatusCode(500);
        //}
    }
}