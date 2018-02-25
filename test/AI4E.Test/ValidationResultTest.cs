using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Test
{
    [TestClass]
    public class ValidationResultTest
    {
        [TestMethod]
        public void ConstructorTest()
        {
            var member = "myMember";
            var message = "myMessage";

            var validationResult = new ValidationResult(member, message);

            Assert.AreEqual(member, validationResult.Member);
            Assert.AreEqual(message, validationResult.Message);
        }

        [TestMethod]
        public void ConstructorTestWithoutMember()
        {
            var message = "myMessage";

            var validationResult = new ValidationResult(message);

            Assert.AreEqual(null, validationResult.Member);
            Assert.AreEqual(message, validationResult.Message);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorFailureTest1()
        {
            string member = null;
            var message = "myMessage";

            var validationResult = new ValidationResult(member, message);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorFailureTest2()
        {
            var member = "myMember";
            string message = null;

            var validationResult = new ValidationResult(member, message);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorFailureTest3()
        {
            string message = null;

            var validationResult = new ValidationResult(message);
        }

        [TestMethod]
        public void EqualityTest()
        {
            var member = "myMember";
            var message = "myMessage";

            var validationResult = new ValidationResult(member, message);

            Assert.IsTrue(validationResult.Equals(validationResult));
            Assert.IsTrue(validationResult.Equals((object)validationResult));
#pragma warning disable CS1718 
            Assert.IsTrue(validationResult == validationResult);
            Assert.IsFalse(validationResult != validationResult);
#pragma warning restore CS1718

            var validationResult2 = validationResult;

            Assert.IsTrue(validationResult.Equals(validationResult2));
            Assert.IsTrue(validationResult2.Equals(validationResult));
            Assert.IsTrue(validationResult.Equals((object)validationResult2));
            Assert.IsTrue(validationResult2.Equals((object)validationResult));
            Assert.IsTrue(validationResult == validationResult2);
            Assert.IsFalse(validationResult != validationResult2);
            Assert.IsTrue(validationResult2 == validationResult);
            Assert.IsFalse(validationResult2 != validationResult);

            validationResult2 = new ValidationResult(member, message);

            Assert.IsTrue(validationResult.Equals(validationResult2));
            Assert.IsTrue(validationResult2.Equals(validationResult));
            Assert.IsTrue(validationResult.Equals((object)validationResult2));
            Assert.IsTrue(validationResult2.Equals((object)validationResult));
            Assert.IsTrue(validationResult == validationResult2);
            Assert.IsFalse(validationResult != validationResult2);
            Assert.IsTrue(validationResult2 == validationResult);
            Assert.IsFalse(validationResult2 != validationResult);

            validationResult = new ValidationResult(message);

            Assert.IsFalse(validationResult.Equals(validationResult2));
            Assert.IsFalse(validationResult2.Equals(validationResult));
            Assert.IsFalse(validationResult.Equals((object)validationResult2));
            Assert.IsFalse(validationResult2.Equals((object)validationResult));
            Assert.IsFalse(validationResult == validationResult2);
            Assert.IsTrue(validationResult != validationResult2);
            Assert.IsFalse(validationResult2 == validationResult);
            Assert.IsTrue(validationResult2 != validationResult);

            validationResult2 = new ValidationResult(message);

            Assert.IsTrue(validationResult.Equals(validationResult2));
            Assert.IsTrue(validationResult2.Equals(validationResult));
            Assert.IsTrue(validationResult.Equals((object)validationResult2));
            Assert.IsTrue(validationResult2.Equals((object)validationResult));
            Assert.IsTrue(validationResult == validationResult2);
            Assert.IsFalse(validationResult != validationResult2);
            Assert.IsTrue(validationResult2 == validationResult);
            Assert.IsFalse(validationResult2 != validationResult);

            Assert.IsFalse(validationResult.Equals(new object()));
        }

        [TestMethod]
        public void HashCodeTest()
        {
            var member = "myMember";
            var message = "myMessage";

            var validationResult = new ValidationResult(member, message);

            Assert.AreEqual(member.GetHashCode() ^ message.GetHashCode(), validationResult.GetHashCode());

            validationResult = new ValidationResult(message);

            Assert.AreEqual(message.GetHashCode(), validationResult.GetHashCode());
        }
    }

    [TestClass]
    public class ValidationResultsBuilderTest
    {
        [TestMethod]
        public void ConstructionTest()
        {
            var builder = new ValidationResultsBuilder();

            var validationResults = builder.GetValidationResults();

            Assert.IsNotNull(validationResults);
            Assert.IsFalse(validationResults.Any());
        }

        [TestMethod]
        public void AdditionTest1()
        {
            var member = "myMember";
            var message = "myMessage";

            var builder = new ValidationResultsBuilder();

            builder.AddValidationResult(member, message);

            var validationResults = builder.GetValidationResults();

            Assert.IsNotNull(validationResults);
            Assert.AreEqual(1, validationResults.Count());
            Assert.AreEqual(member, validationResults.First().Member);
            Assert.AreEqual(message, validationResults.First().Message);

            builder.AddValidationResult(member, message);

            validationResults = builder.GetValidationResults();

            Assert.IsNotNull(validationResults);
            Assert.AreEqual(1, validationResults.Count());
            Assert.AreEqual(member, validationResults.First().Member);
            Assert.AreEqual(message, validationResults.First().Message);
        }

        [TestMethod]
        public void AdditionTest2()
        {
            var message = "myMessage";

            var builder = new ValidationResultsBuilder();

            builder.AddValidationResult(message);

            var validationResults = builder.GetValidationResults();

            Assert.IsNotNull(validationResults);
            Assert.AreEqual(1, validationResults.Count());
            Assert.AreEqual(null, validationResults.First().Member);
            Assert.AreEqual(message, validationResults.First().Message);

            builder.AddValidationResult(message);

            validationResults = builder.GetValidationResults();

            Assert.IsNotNull(validationResults);
            Assert.AreEqual(1, validationResults.Count());
            Assert.AreEqual(null, validationResults.First().Member);
            Assert.AreEqual(message, validationResults.First().Message);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AdditionFailureTest1()
        {
            string message = null;

            var builder = new ValidationResultsBuilder();

            builder.AddValidationResult(message);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AdditionFailureTest2()
        {
            string member = null;
            var message = "myMessage";

            var builder = new ValidationResultsBuilder();

            builder.AddValidationResult(message, member);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AdditionFailureTest3()
        {
            var member = "myMember";
            string message = null;

            var builder = new ValidationResultsBuilder();

            builder.AddValidationResult(message, member);
        }
    }
}
