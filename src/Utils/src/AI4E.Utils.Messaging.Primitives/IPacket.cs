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

namespace AI4E.Utils.Messaging.Primitives
{
    public interface IPacket
    {
        Message Message { get; }

        IPacket WithMessage(in Message message);
    }

    public interface IPacket<TPacket> : IPacket
        where TPacket : IPacket
    {
        new TPacket WithMessage(in Message message);

#if SUPPORTS_DEFAULT_INTERFACE_METHODS
        IPacket IPacket.WithMessage(in Message message)
        {
            return WithMessage(message);
        }
#endif
    }

    public readonly struct Packet : IPacket<Packet>
    {
        public Packet(Message message)
        {
            Message = message;
        }

        public Packet WithMessage(in Message message)
        {
            return new Packet(message);
        }

        public Message Message { get; }

        IPacket IPacket.WithMessage(in Message message)
        {
            return new Packet(message);
        }
    }
}
