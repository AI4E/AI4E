/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System.Text;
using AI4E.Remoting;

namespace AI4E.Coordination.Utils
{
    public sealed class StringAddressConversion : IAddressConversion<StringAddress>
    {
        public byte[] SerializeAddress(StringAddress route)
        {
            return Encoding.UTF8.GetBytes(route.Address);
        }

        public StringAddress DeserializeAddress(byte[] buffer)
        {
            return new StringAddress(Encoding.UTF8.GetString(buffer));
        }

        public string ToString(StringAddress route)
        {
            return route.Address;
        }

        public StringAddress Parse(string str)
        {
            return new StringAddress(str);
        }
    }
}
