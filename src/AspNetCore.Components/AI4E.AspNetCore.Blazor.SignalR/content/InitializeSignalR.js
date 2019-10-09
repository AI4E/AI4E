"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const ServerSentEventsTransport_1 = require("./ServerSentEventsTransport");
const WebSocketsTransport_1 = require("./WebSocketsTransport");
var SignalR;
(function (SignalR) {
    const moduleName = 'AI4E.AspNetCore.Blazor.SignalR';
    const extensionObject = {
        ServerSentEventsTransport: new ServerSentEventsTransport_1.ServerSentEventsTransport(),
        WebSocketsTransport: new WebSocketsTransport_1.WebSocketsTransport()
    };
    function initialize() {
        if (typeof window !== 'undefined' && !window[moduleName]) {
            window[moduleName] = Object.assign({}, extensionObject);
        }
        else {
            window[moduleName] = Object.assign(Object.assign({}, window[moduleName]), extensionObject);
        }
    }
    SignalR.initialize = initialize;
})(SignalR || (SignalR = {}));
SignalR.initialize();
//# sourceMappingURL=InitializeSignalR.js.map