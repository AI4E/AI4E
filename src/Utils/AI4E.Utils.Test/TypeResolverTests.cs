using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils
{
    [TestClass]
    public sealed class TypeResolverTests
    {
        private TypeResolver TypeResolver { get; set; }

        [TestInitialize]
        public void Setup()
        {
            TypeResolver = GetTypeLoader(fallbackToDefaultContext: false);
        }

        private TypeResolver GetTypeLoader(bool fallbackToDefaultContext)
        {
            var assemblies = new Assembly[] { Assembly.GetExecutingAssembly(), typeof(TypeResolver).Assembly };
            return new TypeResolver(assemblies, fallbackToDefaultContext);
        }

        [TestMethod]
        public void SimpleTypeTest()
        {
            var expectedType = typeof(TypeResolverTests);
            var unqualifiedTypeName = expectedType.GetUnqualifiedTypeName();

            var success = TypeResolver.TryResolveType(unqualifiedTypeName, out var type);

            Assert.IsTrue(success);
            Assert.IsNotNull(type);
            Assert.AreSame(expectedType, type);
        }

        [TestMethod]
        public void ArrayTest()
        {
            var expectedType = typeof(TypeResolverTests[]);
            var unqualifiedTypeName = expectedType.GetUnqualifiedTypeName();

            var success = TypeResolver.TryResolveType(unqualifiedTypeName, out var type);

            Assert.IsTrue(success);
            Assert.IsNotNull(type);
            Assert.AreSame(expectedType, type);
        }

        [TestMethod]
        public void MultidimensionalArrayTest()
        {
            var expectedType = typeof(TypeResolverTests[,]);
            var unqualifiedTypeName = expectedType.GetUnqualifiedTypeName();

            var success = TypeResolver.TryResolveType(unqualifiedTypeName, out var type);

            Assert.IsTrue(success);
            Assert.IsNotNull(type);
            Assert.AreSame(expectedType, type);
        }

        [TestMethod]
        public void GenericTypeTest()
        {
            var expectedType = typeof(OrderedSet<TypeResolverTests>);
            var unqualifiedTypeName = expectedType.GetUnqualifiedTypeName();

            var success = TypeResolver.TryResolveType(unqualifiedTypeName, out var type);

            Assert.IsTrue(success);
            Assert.IsNotNull(type);
            Assert.AreSame(expectedType, type);
        }

        [TestMethod]
        public void ArrayOfGenericTypeTest()
        {
            var expectedType = typeof(OrderedSet<TypeResolverTests>[]);
            var unqualifiedTypeName = expectedType.GetUnqualifiedTypeName();

            var success = TypeResolver.TryResolveType(unqualifiedTypeName, out var type);

            Assert.IsTrue(success);
            Assert.IsNotNull(type);
            Assert.AreSame(expectedType, type);
        }

        [TestMethod]
        public void MultidimensionalArrayOfGenericTypeTest()
        {
            var expectedType = typeof(OrderedSet<TypeResolverTests>[,]);
            var unqualifiedTypeName = expectedType.GetUnqualifiedTypeName();

            var success = TypeResolver.TryResolveType(unqualifiedTypeName, out var type);

            Assert.IsTrue(success);
            Assert.IsNotNull(type);
            Assert.AreSame(expectedType, type);
        }

        [TestMethod]
        public void GenericTypeOfArrayTest()
        {
            var expectedType = typeof(OrderedSet<TypeResolverTests[]>);
            var unqualifiedTypeName = expectedType.GetUnqualifiedTypeName();

            var success = TypeResolver.TryResolveType(unqualifiedTypeName, out var type);

            Assert.IsTrue(success);
            Assert.IsNotNull(type);
            Assert.AreSame(expectedType, type);
        }

        [TestMethod]
        public void GenericTypeOfMultidimensionalArrayTest()
        {
            var expectedType = typeof(OrderedSet<TypeResolverTests[,]>);
            var unqualifiedTypeName = expectedType.GetUnqualifiedTypeName();

            var success = TypeResolver.TryResolveType(unqualifiedTypeName, out var type);

            Assert.IsTrue(success);
            Assert.IsNotNull(type);
            Assert.AreSame(expectedType, type);
        }

        [TestMethod]
        public void UnloadableTypeTest()
        {
            var expectedType = typeof(OrderedSet<string>);
            var unqualifiedTypeName = expectedType.GetUnqualifiedTypeName();

            var success = TypeResolver.TryResolveType(unqualifiedTypeName, out var type);

            Assert.IsFalse(success);
            Assert.IsNull(type);
        }

        [TestMethod]
        public void FallbackTest()
        {
            var typeLoader = GetTypeLoader(fallbackToDefaultContext: true);
            var expectedType = typeof(OrderedSet<string>);
            var unqualifiedTypeName = expectedType.GetUnqualifiedTypeName();

            var success = typeLoader.TryResolveType(unqualifiedTypeName, out var type);

            Assert.IsTrue(success);
            Assert.IsNotNull(type);
            Assert.AreSame(expectedType, type);
        }
    }
}
