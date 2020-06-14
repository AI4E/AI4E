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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * Fast Deep Copy by Expression Trees 
 * https://www.codeproject.com/articles/1111658/fast-deep-copy-by-expression-trees-c-sharp
 * 
 * MIT License
 * 
 * Copyright (c) 2014 - 2016 Frantisek Konopecky
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using AI4E.Utils.ObjectClone.Test.TestTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.ObjectClone.Test
{
    [TestClass]
    public class SimpleClassTests
    {
        [TestMethod]
        public void Test1()
        {
            var s = SimpleClass.CreateForTests(1);
            var sCopy = (SimpleClass)CopyFunctionSelection._copyMethod(s);

            // test that the copy is a different instance but with equal content
            Assert_AreEqualButNotSame(s, sCopy);

            // test of method "CreateForTests" that it creates different content
            var s2 = SimpleClass.CreateForTests(2);
            Assert.AreNotEqual(s._fieldPublic, s2._fieldPublic);
            Assert.AreNotEqual(s.PropertyPublic, s2.PropertyPublic);
            Assert.AreNotEqual(s._readOnlyField, s2._readOnlyField);
            Assert.AreNotEqual(s.GetPrivateField(), s2.GetPrivateField());
            Assert.AreNotEqual(s.GetPrivateProperty(), s2.GetPrivateProperty());
        }

        public static void Assert_AreEqualButNotSame(SimpleClass s, SimpleClass sCopy)
        {
            if (s == null && sCopy == null)
            {
                return;
            }

            // copy is different instance
            Assert.AreNotSame(s, sCopy);

            // values in properties and values are the same
            Assert.AreEqual(s._fieldPublic, sCopy._fieldPublic);
            Assert.AreEqual(s.PropertyPublic, sCopy.PropertyPublic);
            Assert.AreEqual(s._readOnlyField, sCopy._readOnlyField);
            Assert.AreEqual(s.GetPrivateField(), sCopy.GetPrivateField());
            Assert.AreEqual(s.GetPrivateProperty(), sCopy.GetPrivateProperty());

            // doublecheck that copy is a different instance
            sCopy._fieldPublic++;
            Assert.AreNotEqual(s._fieldPublic, sCopy._fieldPublic);
            sCopy._fieldPublic--;
        }
    }
}
