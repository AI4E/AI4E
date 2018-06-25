/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        MessagingOptions.cs 
 * Types:           AI4E.MessagingOptions
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   26.08.2017 
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

using System.Collections.Generic;
using AI4E.Validation;

namespace AI4E
{
    public class MessagingOptions
    {
        public MessagingOptions()
        {
            MessageProcessors = new List<IContextualProvider<IMessageProcessor>>()
            {
                ContextualProvider.Create<ValidationCommandProcessor>()
            };
        }

        public IList<IContextualProvider<IMessageProcessor>> MessageProcessors { get; }
    }
}
