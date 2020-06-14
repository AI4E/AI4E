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

using System;
using System.Linq;
using AI4E.Utils.ObjectClone.Test.TestTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.ObjectClone.Test
{
    [TestClass]
    public class ComplexClassTests
    {
        [TestMethod]
        public void Test1()
        {
            var c = ComplexClass.CreateForTests();
            var cCopy = (ComplexClass)CopyFunctionSelection._copyMethod(c);

            // test that the copy is a different instance but with equal content
            Assert_AreEqualButNotSame(c, cCopy);

            // test that the same subobjects should remain the same in a copy (we put same objects to different dictionaries)
            Assert.AreSame(cCopy._sampleDictionary[typeof(ComplexClass).ToString()],
                                                 cCopy.ISampleDictionary[typeof(ComplexClass).ToString()]);
            Assert.AreSame(cCopy._sampleDictionary[typeof(ModerateClass).ToString()],
                                                 cCopy.ISampleDictionary[typeof(ModerateClass).ToString()]);
            Assert.AreNotSame(cCopy._sampleDictionary[typeof(SimpleClass).ToString()],
                                                 cCopy.ISampleDictionary[typeof(SimpleClass).ToString()]);
            Assert.AreSame(cCopy._iSimpleMultiDimArray[0, 0, 0], cCopy._simpleMultiDimArray[1][1][1]);
        }
        
        public static void Assert_AreEqualButNotSame(ComplexClass c, ComplexClass cCopy)
        {
            if (c == null && cCopy == null)
            {
                return;
            }

            // objects are different instances
            Assert.AreNotSame(c, cCopy);

            // test on circular references
            Assert.AreSame(cCopy, cCopy.ThisComplexClass);
            Assert.AreSame(cCopy, cCopy.TupleOfThis.Item1);
            Assert.AreSame(cCopy, cCopy.TupleOfThis.Item2);
            Assert.AreSame(cCopy, cCopy.TupleOfThis.Item3);

            // original had nonnull delegates and events but copy has it null (for ExpressionTree copy method)
            Assert.IsTrue(c._justDelegate != null);
            Assert.IsTrue(cCopy._justDelegate == null); 
            Assert.IsTrue(c._readonlyDelegate != null);
            Assert.IsTrue(cCopy._readonlyDelegate == null);
            Assert.IsTrue(!c.IsJustEventNull);
            Assert.IsTrue(cCopy.IsJustEventNull);

            // test of regular dictionary
            Assert.AreEqual(c._sampleDictionary.Count, cCopy._sampleDictionary.Count);
            
            foreach (var pair in c._sampleDictionary.Zip(cCopy._sampleDictionary, (item, itemCopy) => new { item, itemCopy }))
            {
                Assert.AreEqual(pair.item.Key, pair.itemCopy.Key);

                Assert_AreEqualButNotSame_ChooseForType(pair.item.Value, pair.itemCopy.Value);
            }

            // test of dictionary of interfaces
            Assert.AreEqual(c.ISampleDictionary.Count, cCopy.ISampleDictionary.Count);

            foreach (var pair in c.ISampleDictionary.Zip(cCopy.ISampleDictionary, (item, itemCopy) => new { item, itemCopy }))
            {
                Assert.AreEqual(pair.item.Key, pair.itemCopy.Key);

                Assert_AreEqualButNotSame_ChooseForType(pair.item.Value, pair.itemCopy.Value);
            }

            // test of [,,] interface array
            if (c._iSimpleMultiDimArray != null)
            {
                Assert.AreEqual(c._iSimpleMultiDimArray.Rank, cCopy._iSimpleMultiDimArray.Rank);

                for (var i = 0; i < c._iSimpleMultiDimArray.Rank; i++)
                {
                    Assert.AreEqual(c._iSimpleMultiDimArray.GetLength(i), cCopy._iSimpleMultiDimArray.GetLength(i));
                }

                foreach (var pair in c._iSimpleMultiDimArray.Cast<ISimpleClass>().Zip(cCopy._iSimpleMultiDimArray.Cast<ISimpleClass>(), (item, itemCopy) => new { item, itemCopy }))
                {
                    Assert_AreEqualButNotSame_ChooseForType(pair.item, pair.itemCopy);
                }
            }

            // test of array of arrays of arrays (SimpleClass[][][])
            if (c._simpleMultiDimArray != null)
            {
                Assert.AreEqual(c._simpleMultiDimArray.Length, cCopy._simpleMultiDimArray.Length);

                for (var i = 0; i < c._simpleMultiDimArray.Length; i++)
                {
                    var subArray = c._simpleMultiDimArray[i];
                    var subArrayCopy = cCopy._simpleMultiDimArray[i];

                    if (subArray != null)
                    {
                        Assert.AreEqual(subArray.Length, subArrayCopy.Length);

                        for (var j = 0; j < subArray.Length; j++)
                        {
                            var subSubArray = subArray[j];
                            var subSubArrayCopy = subArrayCopy[j];

                            if (subSubArray != null)
                            {
                                Assert.AreEqual(subSubArray.Length, subSubArrayCopy.Length);

                                for (var k = 0; k < subSubArray.Length; k++)
                                {
                                    var item = subSubArray[k];
                                    var itemCopy = subSubArrayCopy[k];

                                    Assert_AreEqualButNotSame_ChooseForType(item, itemCopy);
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void Assert_AreEqualButNotSame_ChooseForType(ISimpleClass s, ISimpleClass sCopy)
        {
            if (s == null && sCopy == null)
            {
                return;
            }

            if (s is ComplexClass)
            {
                Assert_AreEqualButNotSame((ComplexClass)s, (ComplexClass)sCopy);
            }
            else if (s is ModerateClass)
            {
                ModerateClassTests.Assert_AreEqualButNotSame((ModerateClass)s, (ModerateClass)sCopy);
            }
            else
            {
                SimpleClassTests.Assert_AreEqualButNotSame((SimpleClass)s, (SimpleClass)sCopy);
            }
        }
    }
}
