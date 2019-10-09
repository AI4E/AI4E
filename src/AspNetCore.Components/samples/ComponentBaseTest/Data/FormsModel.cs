using System;

namespace ComponentBaseTest.Data
{
    public sealed class FormsModel
    {
        public Guid Id { get; set; }
        public string String { get; set; } = "abc";
        public int Int { get; set; } = 999;
    }
}
