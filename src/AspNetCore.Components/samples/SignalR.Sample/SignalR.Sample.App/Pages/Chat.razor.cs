/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E.AspNetCore.Components.Extensions)
 *
 * MIT License
 *
 * Copyright (c) 2019 Andreas Truetschel and contributors.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * BlazorSignalR (https://github.com/csnewman/BlazorSignalR)
 *
 * MIT License
 *
 * Copyright (c) 2018 csnewman
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace SignalR.Sample.App.Pages
{
    public abstract class Chat_ : ComponentBase
    {
        [Inject] private HttpClient Http { get; set; }
        [Inject] private ILogger<Chat_> Logger { get; set; }
        [Inject] private IJSRuntime JsRuntime { get; set; }
        [Inject] private NavigationManager NavigationManager { get; set; }
        private protected string ToEverybody { get; set; }
        private protected string ToConnection { get; set; }
        private protected string ConnectionId { get; set; }
        private protected string ToMe { get; set; }
        private protected string ToGroup { get; set; }
        private protected string GroupName { get; set; }
        private protected List<string> Messages { get; set; } = new List<string>();

        private IDisposable _objectHandle;
        private IDisposable _listHandle;
        private HubConnection _connection;

        private protected Chat_() { }

        protected override async Task OnInitializedAsync()
        {
            var factory = new HubConnectionBuilder();

            factory.Services.AddLogging(builder => builder
                //.AddBrowserConsole() // Add Blazor.Extensions.Logging.BrowserConsoleLogger // This is not yet available for Blazor 0.8.0 https://github.com/BlazorExtensions/Logging/pull/22
                .SetMinimumLevel(LogLevel.Trace)
            );

            factory.WithUrlBlazor("/chathub", JsRuntime, NavigationManager, options: opt =>
            {
                //opt.Transports = HttpTransportType.WebSockets;
                //opt.SkipNegotiation = true;
                opt.AccessTokenProvider = async () =>
                {
                    var token = await GetJwtToken("DemoUser");
                    Logger?.LogInformation($"Access Token: {token}");
                    return token;
                };
            });

            _connection = factory.Build();

            _connection.On<string>("Send", HandleTest);

            _connection.Closed += exception =>
            {
                Logger?.LogError(exception, "Connection was closed!");
                return Task.CompletedTask;
            };
            await _connection.StartAsync();
        }

        private void HandleTest(string obj)
        {
            Handle(obj);
        }

        public void DemoMethodObject(DemoData data)
        {
            Logger?.LogInformation("Got object!");
            Logger?.LogInformation(data?.GetType().FullName ?? "<NULL>");
            _objectHandle.Dispose();
            if (data == null) return;
            Handle(data);
        }

        public void DemoMethodList(DemoData[] data)
        {
            Logger?.LogInformation("Got List!");
            Logger?.LogInformation(data?.GetType().FullName ?? "<NULL>");
            _listHandle.Dispose();
            if (data == null) return;
            Handle(data);
        }

        private async Task<string> GetJwtToken(string userId)
        {
            var httpResponse = await Http.GetAsync($"{ GetBaseAddress()}generatetoken?user={userId}");
            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsStringAsync();
        }

        private string GetBaseAddress()
        {
            var baseAddress = Http.BaseAddress.ToString();

            if (!baseAddress.EndsWith("/"))
            {
                baseAddress += "/";
            }

            return baseAddress;
        }

        private void Handle(object msg)
        {
            Logger?.LogInformation(msg.ToString());
            Messages.Add(msg.ToString());
            StateHasChanged();
        }

        internal async Task Broadcast()
        {
            await _connection.InvokeAsync("Send", ToEverybody);
        }

        internal async Task SendToOthers()
        {
            await _connection.InvokeAsync("SendToOthers", ToEverybody);
        }

        internal async Task SendToConnection()
        {
            await _connection.InvokeAsync("SendToConnection", ConnectionId, ToConnection);
        }

        internal async Task SendToMe()
        {
            await _connection.InvokeAsync("Echo", ToMe);
        }

        internal async Task SendToGroup()
        {
            await _connection.InvokeAsync("SendToGroup", GroupName, ToGroup);
        }

        internal async Task SendToOthersInGroup()
        {
            await _connection.InvokeAsync("SendToOthersInGroup", GroupName, ToGroup);
        }

        internal async Task JoinGroup()
        {
            await _connection.InvokeAsync("JoinGroup", GroupName);
        }

        internal async Task LeaveGroup()
        {
            await _connection.InvokeAsync("LeaveGroup", GroupName);
        }

        internal async Task TellHubToDoStuff()
        {
            _objectHandle = _connection.On<DemoData>("DemoMethodObject", DemoMethodObject);
            _listHandle = _connection.On<DemoData[]>("DemoMethodList", DemoMethodList);
            await _connection.InvokeAsync("DoSomething");
        }
    }
}
