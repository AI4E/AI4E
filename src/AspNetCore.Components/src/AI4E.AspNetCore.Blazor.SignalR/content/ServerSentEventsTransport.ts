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

export class ServerSentEventsTransport
{
    private connections: Map<string, EventSource> = new Map<string, EventSource>();

    public CreateConnection = (url: string, managedObj: DotNetReferenceType): void =>
    {
        const id = managedObj.invokeMethod<string>("get_InternalSSEId");
        const token = managedObj.invokeMethod<string>("get_SSEAccessToken");

        if (token)
        {
            url += (url.indexOf("?") < 0 ? "?" : "&") + `access_token=${encodeURIComponent(token)}`;
        }

        const eventSource = new EventSource(url, { withCredentials: true });
        this.connections.set(id, eventSource);

        eventSource.onmessage = (e: MessageEvent) =>
        {
            managedObj.invokeMethod<void>("HandleSSEMessage", btoa(e.data));
        };

        eventSource.onerror = (e: Event) =>
        {
            const error = new Error("Error occurred");
            managedObj.invokeMethod<void>("HandleSSEError", error.message);
        };

        eventSource.onopen = () =>
        {
            managedObj.invokeMethod<void>("HandleSSEOpened");
        };
    }

    public CloseConnection = (managedObj: DotNetReferenceType): void =>
    {
        const id = managedObj.invokeMethod<string>("get_InternalSSEId");

        const eventSource = this.connections.get(id);

        if (!eventSource)
            return;

        this.connections.delete(id);

        eventSource.close();
    }

    public IsSupported = (): boolean =>
    {
        return typeof EventSource !== "undefined";
    }
}