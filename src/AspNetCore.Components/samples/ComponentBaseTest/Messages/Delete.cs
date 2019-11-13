using System;
using Newtonsoft.Json;

namespace ComponentBaseTest.Messages
{
    public abstract class Delete
    {
        private protected Delete(Type dataType, object data)
        {
            DataType = dataType;
            Data = data;
        }

        [JsonIgnore]
        public Type DataType { get; }

        [JsonIgnore]
        public object Data { get; }

        public static Delete<TData> Create<TData>(TData data)
            where TData : class
        {
            return new Delete<TData>(data);
        }
    }

    public sealed class Delete<TData> : Delete
        where TData : class
    {
        public Delete(TData data) : base(typeof(TData), data)
        {
            Data = data;
        }

        public new TData Data { get; }
    }
}
