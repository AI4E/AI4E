/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IAddressConversion.cs 
 * Types:           AI4E.Remoting.IAddressConversion'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

namespace AI4E.Remoting
{
    public interface IAddressConversion<TAddress>
    {
        byte[] SerializeAddress(TAddress route); // TODO: Rename parameter to address
        TAddress DeserializeAddress(byte[] buffer); // TODO: Rename parameter to serializedAddress

        string ToString(TAddress route); // TODO: Rename parameter to address
        TAddress Parse(string str); // TODO: Rename parameter to stringifiedAddress
    }
}
