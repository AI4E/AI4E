using System;
using Newtonsoft.Json;

namespace AI4E.Messaging
{
    /// <summary>
    /// A message base-type that is used to update a data-item.
    /// </summary>
    public abstract class Update
    {
        private protected Update(Type dataType, object data)
        {
            DataType = dataType;
            Data = data;
        }

        /// <summary>
        /// Gets the type of the data-item to update.
        /// </summary>
        [JsonIgnore]
        public Type DataType { get; }

        /// <summary>
        /// Gets the data-item that shall be updated in the dispatch operation.
        /// </summary>
        [JsonIgnore]
        public object Data { get; }
    }

    /// <summary>
    /// A message type that is used to update a data-item.
    /// </summary>
    /// <typeparam name="TData">The type of data-item to update.</typeparam>
    public sealed class Update<TData> : Update
        where TData : class
    {
        /// <summary>
        /// Creates a new instance of the <see cref="Update"/> type.
        /// </summary>
        /// <param name="data">The data-item that shall be updated in the dispatch operation.</param>
        [JsonConstructor]
        public Update(TData data) : base(typeof(TData), data)
        {
            Data = data;
        }

        /// <summary>
        /// Gets the data-item that shall be updated in the dispatch operation.
        /// </summary>
        public new TData Data { get; }
    }
}
