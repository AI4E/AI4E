using System;
using System.Threading.Tasks;

namespace AI4E.Routing.SignalR.Client
{
    /*internal*/
    public interface ICallStub
    {
        // The Blazor SignalR client currently only supports handlers with a single argument.
        // TODO: When https://github.com/BlazorExtensions/SignalR/pull/14 is merged and published, we can investigate here.
        Task DeliverAsync(DeliverAsyncArgs args /*int seqNum, byte[] bytes*/);
        Task AckAsync(int seqNum);
    }

    /*internal*/
    public interface IServerCallStub : ICallStub
    {
        Task<string> InitAsync(string previousAddress);
    }

    public sealed class DeliverAsyncArgs
    {
        public int SeqNum { get; set; }
        public string Bytes { get; set; }
    }
}
