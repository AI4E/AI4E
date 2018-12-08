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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * AsyncEnumerator (https://github.com/Andrew-Hanlon/AsyncEnumerator)
 * MIT License
 * 
 * Copyright (c) 2017 Andrew Hanlon
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test.Async
{
    [TestClass]
    public class AsyncEnumeratorTests
    {
        [TestMethod]
        [ExpectedException(typeof(Exception), "Awaiting failed Task did not throw.")]
        public async Task ThrowsOnMoveNext()
        {
            var seq = ExceptionTest1();
            while (await seq.MoveNext()) { }
        }

        [TestMethod]
        public async Task EnumerationAdvancesCorrectlyAndCompletes1()
        {
            var seq = Test1();

            await seq.MoveNext();
            Assert.AreEqual(seq.Current, 1, $"First call to {nameof(seq.MoveNext)} did not advance the enumeration correctly.");

            await seq.MoveNext();
            Assert.AreEqual(seq.Current, 2, $"Call to {nameof(seq.MoveNext)} did not advance the enumeration correctly.");

            await seq.MoveNext();
            Assert.AreEqual(seq.Current, 3, $"Call to {nameof(seq.MoveNext)} did not advance the enumeration correctly.");

            Assert.IsFalse(await seq.MoveNext(), $"Call to {nameof(seq.MoveNext)} did not return false after enumeration completed.");

            Assert.IsTrue(seq.IsCompleted, "Enumeration did not complete after return.");
        }

        [TestMethod]
        public async Task EnumerationAdvancesCorrectlyAndCompletes2()
        {
            var seq = Test2();

            await seq.MoveNext();
            Assert.AreEqual(seq.Current, 1, $"First call to {nameof(seq.MoveNext)} did not advance the enumeration correctly.");

            await seq.MoveNext();
            Assert.AreEqual(seq.Current, 2, $"Call to {nameof(seq.MoveNext)} did not advance the enumeration correctly.");

            await seq.MoveNext();
            Assert.AreEqual(seq.Current, 3, $"Call to {nameof(seq.MoveNext)} did not advance the enumeration correctly.");

            Assert.IsFalse(await seq.MoveNext(), $"Call to {nameof(seq.MoveNext)} did not return false after enumeration completed.");

            Assert.IsTrue(seq.IsCompleted, "Enumeration did not complete after return.");
        }

        [TestMethod]
        public async Task EmptyEnumeratorTest()
        {
            var seq = GetEmptyEnumerator();

            Assert.IsFalse(await seq.MoveNext(), $"Call to {nameof(seq.MoveNext)} did not return false after enumeration completed.");

            Assert.IsTrue(seq.IsCompleted, "Enumeration did not complete after return.");
        }

        private static async AsyncEnumerator<int> ExceptionTest1()
        {
            var yield = await AsyncEnumerator<int>.Capture();

            await yield.Return(1);

            throw new Exception();
        }

        private static async AsyncEnumerator<int> Test1()
        {
            var yield = await AsyncEnumerator<int>.Capture();

            await yield.Return(1);

            await yield.Return(2);

            await yield.Return(3);

            return yield.Break();
        }

        private static async AsyncEnumerator<int> Test2()
        {
            var yield = await AsyncEnumerator<int>.Capture();

            for (var i = 1; i <= 3; i++)
            {
                await yield.Return(i);
            }

            return yield.Break();
        }

        private static async AsyncEnumerator<int> GetEmptyEnumerator()
        {
            var yield = await AsyncEnumerator<int>.Capture();

            return yield.Break();
        }
    }
}
