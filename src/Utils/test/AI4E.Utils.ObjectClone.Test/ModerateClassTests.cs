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
    public class ModerateClassTests
    {
        [TestMethod]
        public void Test1()
        {
            var m = ModerateClass.CreateForTests(1);
            var mCopy = (ModerateClass)CopyFunctionSelection._copyMethod(m);

            // test that the copy is a different instance but with equal content
            Assert_AreEqualButNotSame(m, mCopy);

            // test of copy if we insert it as interface
            var mAsCopiedAsInterface = (ModerateClass)CopyFunctionSelection._copyMethod((ISimpleClass)m);
            Assert_AreEqualButNotSame(m, mAsCopiedAsInterface);
        }

        public static void Assert_AreEqualButNotSame(ModerateClass m, ModerateClass mCopy)
        {
            if (m == null && mCopy == null)
            {
                return;
            }

            // original and copy are different instances
            Assert.AreNotSame(m, mCopy);

            // the same values in fields
            Assert.AreEqual(m._fieldPublic2, mCopy._fieldPublic2);
            Assert.AreEqual(m.PropertyPublic2, mCopy.PropertyPublic2);
            Assert.AreEqual(m._fieldPublic, mCopy._fieldPublic);
            Assert.AreEqual(m.GetPrivateField2(), mCopy.GetPrivateField2());
            Assert.AreEqual(m.GetPrivateProperty2(), mCopy.GetPrivateProperty2());
            Assert.AreEqual(m.GetPrivateField(), mCopy.GetPrivateField());
            Assert.AreEqual(m.GetPrivateProperty(), mCopy.GetPrivateProperty());
            Assert.AreEqual((string)m.ObjectTextProperty, (string)mCopy.ObjectTextProperty);

            // check that structs copied well (but with different instances of subclasses)
            Assert_StructsAreEqual(m._structField, mCopy._structField);

            // chech that classes in structs in structs are copied well
            Assert_DeeperStructsAreEqual(m._deeperStructField, mCopy._deeperStructField);

            // generic classes are well copied
            Assert_GenericClassesAreEqual(m._genericClassField, mCopy._genericClassField);

            // subclass in property copied well
            SimpleClassTests.Assert_AreEqualButNotSame(m.SimpleClassProperty, mCopy.SimpleClassProperty);

            // subclass in readonly field copied well
            SimpleClassTests.Assert_AreEqualButNotSame(m._readonlySimpleClassField, mCopy._readonlySimpleClassField);

            // array of subclasses copied well
            if (m.SimpleClassArray != null)
            {
                Assert.AreEqual(m.SimpleClassArray.Length, mCopy.SimpleClassArray.Length);

                for (var i = 0; i < m.SimpleClassArray.Length; i++)
                {
                    SimpleClassTests.Assert_AreEqualButNotSame(m.SimpleClassArray[i], mCopy.SimpleClassArray[i]);
                }
            }
        }

        public static void Assert_StructsAreEqual(Struct s, Struct sCopy)
        {
            // values are same and then are not the same
            Assert.AreEqual(s.GetItem1(), sCopy.GetItem1());
            sCopy.IncrementItem1();
            Assert.AreNotEqual(s.GetItem1(), sCopy.GetItem1());
            sCopy.DecrementItem1();
            
            // Item23 and Item32 in struct should be the same instance (see constructor of Struct)
            Assert.AreSame(sCopy._item23, sCopy._item32);

            // reference field test
            if (s._item23 != null)
            {
                SimpleClassTests.Assert_AreEqualButNotSame(s._item23, sCopy._item23);
            }

            // reference field test
            if (s._item32 != null)
            {
                SimpleClassTests.Assert_AreEqualButNotSame(s._item32, sCopy._item32);
            }

            // readonly reference field test
            if (s._item4 != null)
            {
                SimpleClassTests.Assert_AreEqualButNotSame(s._item4, sCopy._item4);
            }
        }

        public static void Assert_DeeperStructsAreEqual(DeeperStruct s, DeeperStruct sCopy)
        {
            // values are same and then are not the same
            Assert.AreEqual(s.GetItem1(), sCopy.GetItem1());
            sCopy.IncrementItem1();
            Assert.AreNotEqual(s.GetItem1(), sCopy.GetItem1());
            sCopy.DecrementItem1();

            // test that deep hidden class in structure of structs was copied well
            if (s.GetItem2() != null)
            {
                SimpleClassTests.Assert_AreEqualButNotSame(s.GetItem2(), sCopy.GetItem2());
            }
        }
        
        public static void Assert_GenericClassesAreEqual(GenericClass<SimpleClass> s, GenericClass<SimpleClass> sCopy)
        {
            if (s == null && sCopy == null)
            {
                return;
            }

            // test that subclass is equal but different instance
            if (s._item1 != null)
            {
                SimpleClassTests.Assert_AreEqualButNotSame(s._item1, sCopy._item1);
            }

            // readonly reference field test
            if (s._item2 != null)
            {
                SimpleClassTests.Assert_AreEqualButNotSame(s._item2, sCopy._item2);
            }
        }
    }
}
