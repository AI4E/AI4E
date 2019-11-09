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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.ObjectPool;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils
{
    [TestClass]
    public class ObjectPoolExtensionTests
    {
        [TestMethod]
        public void DiposingDefaultPooledObjectReturnerShallNotThrowTest()
        {
            var defaultReturner = default(PooledObjectReturner<object>);
            defaultReturner.Dispose();
        }

        [TestMethod]
        public void PooledObjectReturnerDisposeTest()
        {
            var poolMock = new ObjectPoolMock();
            var pooledObject = new object();
            var objectReturner = new PooledObjectReturner<object>(poolMock, pooledObject);
            objectReturner.Dispose();

            Assert.AreSame(pooledObject, poolMock._objects.Single());
        }

        [TestMethod]
        public void PooledObjectReturnerDoubleDisposeTest()
        {
            var poolMock = new ObjectPoolMock();
            var pooledObject = new object();
            var objectReturner = new PooledObjectReturner<object>(poolMock, pooledObject);
            objectReturner.Dispose();
            objectReturner.Dispose();

            Assert.AreSame(pooledObject, poolMock._objects.Single());
        }

        [TestMethod]
        public void PooledObjectReturnerCopyTest()
        {
            var poolMock = new ObjectPoolMock();
            var pooledObject = new object();
            var objectReturner = new PooledObjectReturner<object>(poolMock, pooledObject);
            Dispose(objectReturner);
            objectReturner.Dispose();

            Assert.AreSame(pooledObject, poolMock._objects.Single());
        }

        private void Dispose(PooledObjectReturner<object> objectReturner) // Copy by value
        {
            objectReturner.Dispose();
        }

        [TestMethod]
        public void ObjectPoolGetExtensionTest()
        {
            var poolMock = new ObjectPoolMock();
            var pooledObject = new object();
            poolMock._objects.Add(pooledObject);
            var objectReturner = poolMock.Get(out var rentedObject);
            objectReturner.Dispose();

            Assert.AreSame(pooledObject, rentedObject);
            Assert.AreSame(pooledObject, poolMock._objects.Single());
        }
    }

    public class ObjectPoolMock : ObjectPool<object>
    {
        public List<object> _objects = new List<object>();

        public override object Get()
        {
            var result = _objects.Last();
            _objects.RemoveAt(_objects.Count - 1);
            return result;
        }

        public override void Return(object obj)
        {
            _objects.Add(obj);
        }
    }
}
