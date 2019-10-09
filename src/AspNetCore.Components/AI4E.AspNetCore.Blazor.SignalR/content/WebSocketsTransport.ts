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

import { DotNetReferenceType } from './DotNet'

export class WebSocketsTransport {
    private static connections: Map<string, WebSocket> = new Map<string, WebSocket>();

    public CreateConnection = (url: string, binary: boolean, managedObj: DotNetReferenceType): void => {
        const id = managedObj.invokeMethod<string>("get_InternalWebSocketId");
        const token = managedObj.invokeMethod<string>("get_WebSocketAccessToken");

        if (token) {
            url += (url.indexOf("?") < 0 ? "?" : "&") + `access_token=${encodeURIComponent(token)}`;
        }

        url = url.replace(/^http/, "ws");

        const webSocket = new WebSocket(url);
        WebSocketsTransport.connections.set(id, webSocket);
        
        if (binary) {
            webSocket.binaryType = "arraybuffer";
        }

        webSocket.onopen = (_event: Event) => {
            managedObj.invokeMethod<void>("HandleWebSocketOpened");
        };

        webSocket.onerror = (event: Event) => {
            const error = (event instanceof ErrorEvent) ? event.error : new Error("Error occured");
            managedObj.invokeMethod<void>("HandleWebSocketError", error.message);
        };

        webSocket.onmessage = (message: MessageEvent) => {
            managedObj.invokeMethod<void>("HandleWebSocketMessage", btoa(message.data));
        };

        webSocket.onclose = (event: CloseEvent) => {
            managedObj.invokeMethod<void>("HandleWebSocketClosed");
        };
    }

    public Send = (data: string, managedObj: DotNetReferenceType): void => {
        const id = managedObj.invokeMethod<string>("get_InternalWebSocketId");
        const webSocket = WebSocketsTransport.connections.get(id);

        if (!webSocket)
            throw new Error("Unknown connection");

        webSocket.send(atob(data));
    }

    public CloseConnection = (managedObj: DotNetReferenceType): void => {
        const id = managedObj.invokeMethod<string>("get_InternalWebSocketId");

        const webSocket = WebSocketsTransport.connections.get(id);

        if (!webSocket)
            return;

        WebSocketsTransport.connections.delete(id);

        webSocket.onclose = () => {};
        webSocket.onmessage = () => {};
        webSocket.onerror = () => {};
        webSocket.close();
    }

    public IsSupported = (): boolean => {
        return typeof WebSocket !== "undefined";
    }
}