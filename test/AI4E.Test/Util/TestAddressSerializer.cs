using System;
using AI4E.Remoting;

namespace AI4E.Test.Util
{
    public sealed class TestAddressSerializer : IAddressConversion<TestAddress>
    {
        public byte[] SerializeAddress(TestAddress route)
        {
            return new[] { (byte)route };
        }

        public TestAddress DeserializeAddress(byte[] buffer)
        {
            return (TestAddress)buffer[0];
        }

        public string ToString(TestAddress route)
        {
            switch (route)
            {
                case TestAddress.X:
                    return "X";
                case TestAddress.Y:
                    return "Y";
                default:
                    throw new ArgumentException();
            }
        }

        public TestAddress Parse(string str)
        {
            switch (str)
            {
                case "X":
                    return TestAddress.X;
                case "Y":
                    return TestAddress.Y;
                default:
                    throw new ArgumentException();
            }
        }
    }
}
