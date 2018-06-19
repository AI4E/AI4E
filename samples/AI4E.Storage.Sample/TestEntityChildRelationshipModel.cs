using System;
using System.Collections.Generic;
using System.Text;

namespace AI4E.Storage.Sample
{
    public sealed class TestEntityChildRelationshipModel
    {
        private string _id;
        public string Id
        {
            get
            {
                if (_id == null)
                {
                    _id = ParentId + ChildId;
                }

                return _id;
            }
            set => _id = value;
        }

        public string ParentId { get; set; }

        public string ChildId { get; set; }
    }
}
