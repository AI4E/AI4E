"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
class WebSocketsTransport {
    constructor() {
        this.CreateConnection = (url, binary, managedObj) => {
            const id = managedObj.invokeMethod("get_InternalWebSocketId");
            const token = managedObj.invokeMethod("get_WebSocketAccessToken");
            if (token) {
                url += (url.indexOf("?") < 0 ? "?" : "&") + `access_token=${encodeURIComponent(token)}`;
            }
            url = url.replace(/^http/, "ws");
            const webSocket = new WebSocket(url);
            WebSocketsTransport.connections.set(id, webSocket);
            if (binary) {
                webSocket.binaryType = "arraybuffer";
            }
            webSocket.onopen = (_event) => {
                managedObj.invokeMethod("HandleWebSocketOpened");
            };
            webSocket.onerror = (event) => {
                const error = (event instanceof ErrorEvent) ? event.error : new Error("Error occured");
                managedObj.invokeMethod("HandleWebSocketError", error.message);
            };
            webSocket.onmessage = (message) => {
                managedObj.invokeMethod("HandleWebSocketMessage", btoa(message.data));
            };
            webSocket.onclose = (event) => {
                managedObj.invokeMethod("HandleWebSocketClosed");
            };
        };
        this.Send = (data, managedObj) => {
            const id = managedObj.invokeMethod("get_InternalWebSocketId");
            const webSocket = WebSocketsTransport.connections.get(id);
            if (!webSocket)
                throw new Error("Unknown connection");
            webSocket.send(atob(data));
        };
        this.CloseConnection = (managedObj) => {
            const id = managedObj.invokeMethod("get_InternalWebSocketId");
            const webSocket = WebSocketsTransport.connections.get(id);
            if (!webSocket)
                return;
            WebSocketsTransport.connections.delete(id);
            webSocket.onclose = () => { };
            webSocket.onmessage = () => { };
            webSocket.onerror = () => { };
            webSocket.close();
        };
        this.IsSupported = () => {
            return typeof WebSocket !== "undefined";
        };
    }
}
exports.WebSocketsTransport = WebSocketsTransport;
WebSocketsTransport.connections = new Map();
//# sourceMappingURL=WebSocketsTransport.js.map