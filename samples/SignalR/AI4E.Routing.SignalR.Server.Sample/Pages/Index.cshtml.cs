using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AI4E.Routing.SignalR.Sample.Common;

namespace AI4E.Routing.SignalR.Server.Sample.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IMessageDispatcher _messageDispatcher;

        public IndexModel(IMessageDispatcher messageDispatcher)
        {
            _messageDispatcher = messageDispatcher;
        }

        public void OnGet()
        {

        }

        [BindProperty]
        public string Message { get; set; }

        public string Result { get; private set; }

        public async Task<IActionResult> OnPostAsync()
        {
            var result = await _messageDispatcher.DispatchAsync(new TestMessage { Message = Message ?? string.Empty });

            Result = result.ToString();

            return Page();
        }
    }
}
