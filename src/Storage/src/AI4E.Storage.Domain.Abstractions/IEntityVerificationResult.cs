using System;
using System.Collections.Generic;
using System.Text;

namespace AI4E.Storage.Domain
{
    public interface IEntityVerificationResult : IEntityLoadResult
    {
        IFoundEntityQueryResult? QueryResult { get; }
    }
}
