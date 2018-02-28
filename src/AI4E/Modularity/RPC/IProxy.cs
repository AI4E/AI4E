using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace AI4E.Modularity.RPC
{
    internal interface IProxy
    {
        object LocalInstance { get; }

        Type ObjectType { get; }
        int Id { get; }

        void Dispose();

        void Register(RPCHost host, int proxyId);
    }

    public interface IProxy<TRemote>
        where TRemote : class
    {
        TRemote LocalInstance { get; }

        Task ExecuteAsync(Expression<Action<TRemote>> expression);
        Task ExecuteAsync(Expression<Func<TRemote, Task>> expression);

        Task<TResult> ExecuteAsync<TResult>(Expression<Func<TRemote, TResult>> expression);
        Task<TResult> ExecuteAsync<TResult>(Expression<Func<TRemote, Task<TResult>>> expression);
    }
}
