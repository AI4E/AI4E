/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2019 Andreas Truetschel and contributors.
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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E
{
    [TestClass]
    public class PublishOnlyAttributeTests
    {
        [TestMethod]
        public void PublishOnlyTest()
        {
            var configBuilder = new MessageHandlerConfigurationBuilder();
            var attribute = new PublishOnlyAttribute();
            attribute.ExecuteConfigureMessageHandler(memberDescriptor: default, configBuilder);
            var config = configBuilder.Build();

            Assert.IsTrue(config.IsEnabled<PublishOnlyMessageHandlerConfiguration>(true));
            Assert.IsTrue(config.IsEnabled<PublishOnlyMessageHandlerConfiguration>(false));
        }

        [TestMethod]
        public void NonPublishOnlyTest()
        {
            var configBuilder = new MessageHandlerConfigurationBuilder();
            var attribute = new PublishOnlyAttribute(false);
            attribute.ExecuteConfigureMessageHandler(memberDescriptor: default, configBuilder);
            var config = configBuilder.Build();

            Assert.IsFalse(config.IsEnabled<PublishOnlyMessageHandlerConfiguration>(true));
            Assert.IsFalse(config.IsEnabled<PublishOnlyMessageHandlerConfiguration>(false));
        }
    }
}
