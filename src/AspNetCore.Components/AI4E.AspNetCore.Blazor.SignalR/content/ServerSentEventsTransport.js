"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
class ServerSentEventsTransport {
    constructor() {
        this.connections = new Map();
        this.CreateConnection = (url, managedObj) => {
            const id = managedObj.invokeMethod("get_InternalSSEId");
            const token = managedObj.invokeMethod("get_SSEAccessToken");
            if (token) {
                url += (url.indexOf("?") < 0 ? "?" : "&") + `access_token=${encodeURIComponent(token)}`;
            }
            const eventSource = new EventSource(url, { withCredentials: true });
            this.connections.set(id, eventSource);
            eventSource.onmessage = (e) => {
                managedObj.invokeMethod("HandleSSEMessage", btoa(e.data));
            };
            eventSource.onerror = (e) => {
                const error = new Error("Error occurred");
                managedObj.invokeMethod("HandleSSEError", error.message);
            };
            eventSource.onopen = () => {
                managedObj.invokeMethod("HandleSSEOpened");
            };
        };
        this.CloseConnection = (managedObj) => {
            const id = managedObj.invokeMethod("get_InternalSSEId");
            const eventSource = this.connections.get(id);
            if (!eventSource)
                return;
            this.connections.delete(id);
            eventSource.close();
        };
        this.IsSupported = () => {
            return typeof EventSource !== "undefined";
        };
    }
}
exports.ServerSentEventsTransport = ServerSentEventsTransport;
//# sourceMappingURL=ServerSentEventsTransport.js.map