using System;
using AI4E.Storage.Domain.Projection.TestTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Storage.Domain.Projection
{
    [TestClass]
    public class ProjectionRegistrationTests
    {
        [TestMethod]
        public void CreateAndFactoryTest()
        {
            var projectionRegistration = new ProjectionRegistration(
                typeof(ProjectionSource), typeof(ProjectionTarget), provider => new ProjectionRegistrationTestProjection(provider));

            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var projection = (ProjectionRegistrationTestProjection)projectionRegistration.CreateProjection(serviceProvider);

            Assert.AreSame(typeof(ProjectionSource), projectionRegistration.EntityType);
            Assert.AreSame(typeof(ProjectionTarget), projectionRegistration.TargetType);
            Assert.AreSame(serviceProvider, projection.ServiceProvider);
        }

        [TestMethod]
        public void CreateAndFactoryInvalidSourceTypeTest()
        {
            var projectionRegistration = new ProjectionRegistration(
                typeof(object), typeof(ProjectionTarget), provider => new ProjectionRegistrationTestProjection(provider));

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                projectionRegistration.CreateProjection(serviceProvider);
            });
        }

        [TestMethod]
        public void CreateAndFactoryInvalidTargetTypeTest()
        {
            var projectionRegistration = new ProjectionRegistration(
                typeof(ProjectionSource), typeof(object), provider => new ProjectionRegistrationTestProjection(provider));

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                projectionRegistration.CreateProjection(serviceProvider);
            });
        }

        [TestMethod]
        public void CreateAndFactoryNullProjectionTest()
        {
            var projectionRegistration = new ProjectionRegistration(
                typeof(ProjectionSource), typeof(ProjectionTarget), provider => null);
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                projectionRegistration.CreateProjection(serviceProvider);
            });
        }

        [TestMethod]
        public void GenericCreateAndFactoryTest()
        {
            var projectionRegistration = new ProjectionRegistration<ProjectionSource, ProjectionTarget>(
                provider => new ProjectionRegistrationTestProjection(provider));

            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var projection = (ProjectionRegistrationTestProjection)projectionRegistration.CreateProjection(serviceProvider);

            Assert.AreSame(typeof(ProjectionSource), ((IProjectionRegistration)projectionRegistration).EntityType);
            Assert.AreSame(typeof(ProjectionTarget), ((IProjectionRegistration)projectionRegistration).TargetType);
            Assert.AreSame(serviceProvider, projection.ServiceProvider);
        }

        [TestMethod]
        public void GenericCreateAndFactoryNullProjectionTest()
        {
            var projectionRegistration = new ProjectionRegistration<ProjectionSource, ProjectionTarget>(provider => null);
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                projectionRegistration.CreateProjection(serviceProvider);
            });
        }
    }
}
