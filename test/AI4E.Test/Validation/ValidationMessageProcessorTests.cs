using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Handler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Validation
{
    [TestClass]
    public class ValidationMessageProcessorTests
    {
        [TestMethod]
        public async Task ValidateValidTest()
        {
            var testMessage = new ValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new ValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(ValidationTestMessageHandler),
                typeof(ValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult));
            Assert.IsTrue(messageHandler.HandleCalled);
            Assert.AreSame(testMessage, messageHandler.HandleMessage);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task ValidateInvalidTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new ValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(ValidationTestMessageHandler),
                typeof(ValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task NoValidateTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new NoValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(NoValidationTestMessageHandler),
                typeof(NoValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult));
            Assert.IsTrue(messageHandler.HandleCalled);
            Assert.AreSame(testMessage, messageHandler.HandleMessage);
            Assert.IsFalse(messageHandler.ValidateCalled);
        }

        [TestMethod]
        public async Task ExplicitValidationResultsBuilderTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new ExplicitValidationResultsBuilderMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(ExplicitValidationResultsBuilderMessageHandler),
                typeof(ExplicitValidationResultsBuilderMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task DerivedValidateValidTest()
        {
            var testMessage = new DerivedValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<DerivedValidationTestMessage>(testMessage);
            var messageHandler = new ValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(DerivedValidationTestMessage),
                typeof(ValidationTestMessageHandler),
                typeof(ValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult));
            Assert.IsTrue(messageHandler.HandleCalled);
            Assert.AreSame(testMessage, messageHandler.HandleMessage);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task DerivedValidateValid2Test()
        {
            var testMessage = new DerivedValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<DerivedValidationTestMessage>(testMessage);
            var messageHandler = new DerivedValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(DerivedValidationTestMessage),
                typeof(DerivedValidationTestMessageHandler),
                typeof(DerivedValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult));
            Assert.IsTrue(messageHandler.HandleCalled);
            Assert.AreSame(testMessage, messageHandler.HandleMessage);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task DerivedValidateInvalidTest()
        {
            var testMessage = new DerivedValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<DerivedValidationTestMessage>(testMessage);
            var messageHandler = new ValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(DerivedValidationTestMessage),
                typeof(ValidationTestMessageHandler),
                typeof(ValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task DerivedValidateInvalid2Test()
        {
            var testMessage = new DerivedValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<DerivedValidationTestMessage>(testMessage);
            var messageHandler = new DerivedValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(DerivedValidationTestMessage),
                typeof(DerivedValidationTestMessageHandler),
                typeof(DerivedValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task MissingValidationTest()
        {
            var testMessage = new ValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new MissingValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(MissingValidationTestMessageHandler),
                typeof(MissingValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };


            var dispatchResult = await validationProcessor.ProcessAsync(
                             dispatchData,
                             d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                             cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult));
            Assert.IsTrue(messageHandler.HandleCalled);
            Assert.AreSame(testMessage, messageHandler.HandleMessage);
            Assert.IsFalse(messageHandler.ValidateCalled);
        }

        [TestMethod]
        public async Task AmbiguousValidationTest()
        {
            var testMessage = new ValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new AmbiguousValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(AmbiguousValidationTestMessageHandler),
                typeof(AmbiguousValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await validationProcessor.ProcessAsync(
                                 dispatchData,
                                 d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                                 cancellation: default);
            });
        }

        [TestMethod]
        public async Task AsyncValidateValidTest()
        {
            var testMessage = new ValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new AsyncValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(AsyncValidationTestMessageHandler),
                typeof(AsyncValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult));
            Assert.IsTrue(messageHandler.HandleCalled);
            Assert.AreSame(testMessage, messageHandler.HandleMessage);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task AsyncValidateInvalidTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new AsyncValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(AsyncValidationTestMessageHandler),
                typeof(AsyncValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task AsyncExplicitValidationResultsBuilderTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new AsyncExplicitValidationResultsBuilderMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(AsyncExplicitValidationResultsBuilderMessageHandler),
                typeof(AsyncExplicitValidationResultsBuilderMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task ServiceInjectionValidationTest()
        {
            var testMessage = new ValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new ServiceInjectionValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(ServiceInjectionValidationTestMessageHandler),
                typeof(ServiceInjectionValidationTestMessageHandler).GetMethod("Handle"));
            var serviceProvider = BuildServiceProvider(services => services.AddSingleton<IService, Service>());
            var validationProcessor = new ValidationMessageProcessor(serviceProvider)
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;
            await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation);

            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
            Assert.IsNotNull(messageHandler.ValidationResults);
            Assert.AreSame(dispatchData, messageHandler.DispatchData);
            Assert.AreSame(dispatchData, messageHandler.GenericDispatchData);
            Assert.AreEqual(cancellation, messageHandler.Cancellation);
            Assert.AreSame(serviceProvider, messageHandler.ServiceProvider);
            Assert.AreSame(serviceProvider.GetRequiredService<IService>(), messageHandler.Service);
        }

        [TestMethod]
        public async Task UnresolvableNonMandatoryServiceInjectionValidationTest()
        {
            var testMessage = new ValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new UnresolvableNonMandatoryServiceInjectionValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(UnresolvableNonMandatoryServiceInjectionValidationTestMessageHandler),
                typeof(UnresolvableNonMandatoryServiceInjectionValidationTestMessageHandler).GetMethod("Handle"));
            var serviceProvider = BuildServiceProvider();
            var validationProcessor = new ValidationMessageProcessor(serviceProvider)
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;
            await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation);

            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
            Assert.IsNull(messageHandler.Service);
        }

        [TestMethod]
        public async Task UnresolvableMandatoryServiceInjectionValidationTest()
        {
            var testMessage = new ValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new UnresolvableMandatoryServiceInjectionValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(UnresolvableMandatoryServiceInjectionValidationTestMessageHandler),
                typeof(UnresolvableMandatoryServiceInjectionValidationTestMessageHandler).GetMethod("Handle"));
            var serviceProvider = BuildServiceProvider();
            var validationProcessor = new ValidationMessageProcessor(serviceProvider)
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellation = cancellationTokenSource.Token;

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await validationProcessor.ProcessAsync(
                    dispatchData,
                    d => throw null,
                    cancellation);
            });
        }

        [TestMethod]
        public async Task CombineValidateInvalidTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new CombineValidationTestMessageHandler(false);
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(CombineValidationTestMessageHandler),
                typeof(CombineValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task CombineValidateNullResultInvalidTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new CombineValidationTestMessageHandler(true);
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(CombineValidationTestMessageHandler),
                typeof(CombineValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task SingleResultCombineValidateInvalidTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new SingleResultCombineValidationTestMessageHandler(false);
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(SingleResultCombineValidationTestMessageHandler),
                typeof(SingleResultCombineValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task SingleResultCombineValidateNullResultInvalidTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new SingleResultCombineValidationTestMessageHandler(true);
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(SingleResultCombineValidationTestMessageHandler),
                typeof(SingleResultCombineValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task SingleResultValidateValidTest()
        {
            var testMessage = new ValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new SingleResultValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(SingleResultValidationTestMessageHandler),
                typeof(SingleResultValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult));
            Assert.IsTrue(messageHandler.HandleCalled);
            Assert.AreSame(testMessage, messageHandler.HandleMessage);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task SingleResultValidateInvalidTest()
        {
            var testMessage = new ValidationTestMessage("myString", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new SingleResultValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(SingleResultValidationTestMessageHandler),
                typeof(SingleResultValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(1, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
            {
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task BuilderResultValidateValidTest()
        {
            var testMessage = new ValidationTestMessage("myString", 243);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new BuilderResultValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(BuilderResultValidationTestMessageHandler),
                typeof(BuilderResultValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(SuccessDispatchResult));
            Assert.IsTrue(messageHandler.HandleCalled);
            Assert.AreSame(testMessage, messageHandler.HandleMessage);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task BuilderResultValidateInvalidTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new BuilderResultValidationTestMessageHandler();
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(BuilderResultValidationTestMessageHandler),
                typeof(BuilderResultValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task CombineBuilderResultValidationTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new CombineBuilderResultValidationTestMessageHandler(false, false);
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(CombineBuilderResultValidationTestMessageHandler),
                typeof(CombineBuilderResultValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task CombineBuilderResultReturnNullValidationTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new CombineBuilderResultValidationTestMessageHandler(true, false);
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(CombineBuilderResultValidationTestMessageHandler),
                typeof(CombineBuilderResultValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        [TestMethod]
        public async Task CombineBuilderResultReturnInjectedValidationTest()
        {
            var testMessage = new ValidationTestMessage("", -1);
            var dispatchData = new DispatchDataDictionary<ValidationTestMessage>(testMessage);
            var messageHandler = new CombineBuilderResultValidationTestMessageHandler(false, true);
            var memberDescriptor = new MessageHandlerActionDescriptor(
                typeof(ValidationTestMessage),
                typeof(CombineBuilderResultValidationTestMessageHandler),
                typeof(CombineBuilderResultValidationTestMessageHandler).GetMethod("Handle"));
            var validationProcessor = new ValidationMessageProcessor(BuildServiceProvider())
            {
                Context = new MessageProcessorContext(messageHandler, memberDescriptor, publish: false, isLocalDispatch: false)
            };

            var dispatchResult = await validationProcessor.ProcessAsync(
                dispatchData,
                d => { messageHandler.Handle(d.Message); return new ValueTask<IDispatchResult>(new SuccessDispatchResult()); },
                cancellation: default);

            Assert.IsInstanceOfType(dispatchResult, typeof(ValidationFailureDispatchResult));
            Assert.AreEqual(2, ((ValidationFailureDispatchResult)dispatchResult).ValidationResults.Count);
            Assert.IsTrue(((ValidationFailureDispatchResult)dispatchResult).ValidationResults.ToHashSet().SetEquals(new[]
             {
                new ValidationResult("Must not be null nor whitespace.", "String"),
                new ValidationResult("Must be non-negative.", "Int"),
            }));
            Assert.IsFalse(messageHandler.HandleCalled);
            Assert.IsTrue(messageHandler.ValidateCalled);
            Assert.AreSame(testMessage, messageHandler.ValidateMessage);
        }

        private IServiceProvider BuildServiceProvider()
        {
            return new ServiceCollection().BuildServiceProvider();
        }

        private IServiceProvider BuildServiceProvider(Action<IServiceCollection> serviceConfiguration)
        {
            var serviceCollection = new ServiceCollection();
            serviceConfiguration(serviceCollection);
            return serviceCollection.BuildServiceProvider();
        }
    }

    public class ValidationTestMessage
    {
        public ValidationTestMessage(string @string, int @int)
        {
            String = @string;
            Int = @int;
        }

        public string String { get; }
        public int Int { get; }
    }

    public class DerivedValidationTestMessage : ValidationTestMessage
    {
        public DerivedValidationTestMessage(string @string, int @int) : base(@string, @int) { }
    }

    public class ValidationTestMessageHandler
    {
        [Validate]
        public void Handle(ValidationTestMessage message)
        {
            HandleMessage = message;
            HandleCalled = true;
        }

        public void Validate(ValidationTestMessage message, ValidationResultsBuilder validationResults)
        {
            ValidateMessage = message;
            ValidateCalled = true;

            if (string.IsNullOrWhiteSpace(message.String))
            {
                validationResults.AddValidationResult("Must not be null nor whitespace.", nameof(message.String));
            }

            if (message.Int < 0)
            {
                validationResults.AddValidationResult("Must be non-negative.", nameof(message.Int));
            }
        }

        public ValidationTestMessage HandleMessage { get; private set; }
        public bool HandleCalled { get; private set; }
        public ValidationTestMessage ValidateMessage { get; private set; }
        public bool ValidateCalled { get; private set; }
    }

    public class ExplicitValidationResultsBuilderMessageHandler
    {
        [Validate]
        public void Handle(ValidationTestMessage message)
        {
            HandleCalled = true;
        }

        public IEnumerable<ValidationResult> Validate(ValidationTestMessage message)
        {
            var validationResults = new ValidationResultsBuilder();

            ValidateMessage = message;
            ValidateCalled = true;

            if (string.IsNullOrWhiteSpace(message.String))
            {
                validationResults.AddValidationResult("Must not be null nor whitespace.", nameof(message.String));
            }

            if (message.Int < 0)
            {
                validationResults.AddValidationResult("Must be non-negative.", nameof(message.Int));
            }

            return validationResults.GetValidationResults();
        }

        public bool HandleCalled { get; private set; }
        public ValidationTestMessage ValidateMessage { get; private set; }
        public bool ValidateCalled { get; private set; }
    }

    public class NoValidationTestMessageHandler
    {
        public void Handle(ValidationTestMessage message)
        {
            HandleMessage = message;
            HandleCalled = true;
        }

        public void Validate(ValidationTestMessage message, ValidationResultsBuilder validationResults)
        {
            ValidateCalled = true;
        }

        public ValidationTestMessage HandleMessage { get; private set; }
        public bool HandleCalled { get; private set; }
        public bool ValidateCalled { get; private set; }
    }

    public class DerivedValidationTestMessageHandler
    {
        [Validate]
        public void Handle(DerivedValidationTestMessage message)
        {
            HandleMessage = message;
            HandleCalled = true;
        }

        public void Validate(ValidationTestMessage message, ValidationResultsBuilder validationResults)
        {
            ValidateMessage = message;
            ValidateCalled = true;

            if (string.IsNullOrWhiteSpace(message.String))
            {
                validationResults.AddValidationResult("Must not be null nor whitespace.", nameof(message.String));
            }

            if (message.Int < 0)
            {
                validationResults.AddValidationResult("Must be non-negative.", nameof(message.Int));
            }
        }

        public DerivedValidationTestMessage HandleMessage { get; private set; }
        public bool HandleCalled { get; private set; }
        public ValidationTestMessage ValidateMessage { get; private set; }
        public bool ValidateCalled { get; private set; }
    }

    public class MissingValidationTestMessageHandler
    {
        [Validate]
        public void Handle(ValidationTestMessage message)
        {
            HandleMessage = message;
            HandleCalled = true;
        }

        public ValidationTestMessage HandleMessage { get; private set; }
        public bool HandleCalled { get; private set; }
        public bool ValidateCalled { get; private set; }
    }

    public class AmbiguousValidationTestMessageHandler
    {
        [Validate]
        public void Handle(ValidationTestMessage message) { }

        public void Validate(ValidationTestMessage message, ValidationResultsBuilder validationResults) { }

        public IEnumerable<ValidationResult> Validate(ValidationTestMessage message) { throw null; }
    }

    public class AsyncValidationTestMessageHandler
    {
        [Validate]
        public void Handle(ValidationTestMessage message)
        {
            HandleMessage = message;
            HandleCalled = true;
        }

        public Task ValidateAsync(ValidationTestMessage message, ValidationResultsBuilder validationResults)
        {
            ValidateMessage = message;
            ValidateCalled = true;

            if (string.IsNullOrWhiteSpace(message.String))
            {
                validationResults.AddValidationResult("Must not be null nor whitespace.", nameof(message.String));
            }

            if (message.Int < 0)
            {
                validationResults.AddValidationResult("Must be non-negative.", nameof(message.Int));
            }

            return Task.CompletedTask;
        }

        public ValidationTestMessage HandleMessage { get; private set; }
        public bool HandleCalled { get; private set; }
        public ValidationTestMessage ValidateMessage { get; private set; }
        public bool ValidateCalled { get; private set; }
    }

    public class AsyncExplicitValidationResultsBuilderMessageHandler
    {
        [Validate]
        public void Handle(ValidationTestMessage message)
        {
            HandleCalled = true;
        }

        public ValueTask<IEnumerable<ValidationResult>> ValidateAsync(ValidationTestMessage message)
        {
            var validationResults = new ValidationResultsBuilder();

            ValidateMessage = message;
            ValidateCalled = true;

            if (string.IsNullOrWhiteSpace(message.String))
            {
                validationResults.AddValidationResult("Must not be null nor whitespace.", nameof(message.String));
            }

            if (message.Int < 0)
            {
                validationResults.AddValidationResult("Must be non-negative.", nameof(message.Int));
            }

            return new ValueTask<IEnumerable<ValidationResult>>(validationResults.GetValidationResults());
        }

        public bool HandleCalled { get; private set; }
        public ValidationTestMessage ValidateMessage { get; private set; }
        public bool ValidateCalled { get; private set; }
    }

    public interface IService { }
    public sealed class Service : IService { }

    public class ServiceInjectionValidationTestMessageHandler
    {
        [Validate]
        public void Handle(ValidationTestMessage message) { }

        public void Validate(ValidationTestMessage message,
            ValidationResultsBuilder validationResults,
            IServiceProvider serviceProvider,
            CancellationToken cancellation,
            DispatchDataDictionary dispatchData,
            DispatchDataDictionary<ValidationTestMessage> genericDispatchData,
            IService service)
        {
            ValidateMessage = message;
            ValidationResults = validationResults;
            ServiceProvider = serviceProvider;
            Cancellation = cancellation;
            DispatchData = dispatchData;
            GenericDispatchData = genericDispatchData;
            Service = service;
        }

        public ValidationTestMessage ValidateMessage { get; private set; }
        public ValidationResultsBuilder ValidationResults { get; private set; }
        public IServiceProvider ServiceProvider { get; private set; }
        public CancellationToken Cancellation { get; private set; }
        public DispatchDataDictionary DispatchData { get; private set; }
        public DispatchDataDictionary<ValidationTestMessage> GenericDispatchData { get; private set; }
        public IService Service { get; private set; }
    }

    public class UnresolvableNonMandatoryServiceInjectionValidationTestMessageHandler
    {
        [Validate]
        public void Handle(ValidationTestMessage message) { }

        public void Validate(ValidationTestMessage message, IService service = null)
        {
            ValidateMessage = message;
            Service = service;
        }

        public ValidationTestMessage ValidateMessage { get; private set; }
        public IService Service { get; private set; }
    }

    public class UnresolvableMandatoryServiceInjectionValidationTestMessageHandler
    {
        [Validate]
        public void Handle(ValidationTestMessage message) { }

        public void Validate(ValidationTestMessage message, IService service)
        {
            throw null;
        }
    }

    public class CombineValidationTestMessageHandler
    {
        public CombineValidationTestMessageHandler(bool returnNull)
        {
            ReturnNull = returnNull;
        }

        [Validate]
        public void Handle(ValidationTestMessage message)
        {
            HandleMessage = message;
            HandleCalled = true;
        }

        public IEnumerable<ValidationResult> Validate(ValidationTestMessage message, ValidationResultsBuilder validationResults)
        {
            ValidateMessage = message;
            ValidateCalled = true;

            if (string.IsNullOrWhiteSpace(message.String))
            {
                validationResults.AddValidationResult("Must not be null nor whitespace.", nameof(message.String));
            }

            var validationResults2 = new ValidationResultsBuilder();

            if (message.Int < 0)
            {
                (ReturnNull ? validationResults : validationResults2).AddValidationResult("Must be non-negative.", nameof(message.Int));
            }

            if (ReturnNull)
                return null;

            return validationResults2.GetValidationResults();
        }

        public ValidationTestMessage HandleMessage { get; private set; }
        public bool HandleCalled { get; private set; }
        public ValidationTestMessage ValidateMessage { get; private set; }
        public bool ValidateCalled { get; private set; }
        public bool ReturnNull { get; }
    }

    public class SingleResultCombineValidationTestMessageHandler
    {
        public SingleResultCombineValidationTestMessageHandler(bool returnNull)
        {
            ReturnNull = returnNull;
        }

        [Validate]
        public void Handle(ValidationTestMessage message)
        {
            HandleMessage = message;
            HandleCalled = true;
        }

        public ValidationResult Validate(ValidationTestMessage message, ValidationResultsBuilder validationResults)
        {
            ValidateMessage = message;
            ValidateCalled = true;

            if (string.IsNullOrWhiteSpace(message.String))
            {
                validationResults.AddValidationResult("Must not be null nor whitespace.", nameof(message.String));
            }

            if (message.Int < 0)
            {
                if (!ReturnNull)
                    return new ValidationResult("Must be non-negative.", nameof(message.Int));
                else
                    validationResults.AddValidationResult("Must be non-negative.", nameof(message.Int));
            }

            return default;
        }

        public ValidationTestMessage HandleMessage { get; private set; }
        public bool HandleCalled { get; private set; }
        public ValidationTestMessage ValidateMessage { get; private set; }
        public bool ValidateCalled { get; private set; }
        public bool ReturnNull { get; }
    }

    public class SingleResultValidationTestMessageHandler
    {
        public SingleResultValidationTestMessageHandler() { }

        [Validate]
        public void Handle(ValidationTestMessage message)
        {
            HandleMessage = message;
            HandleCalled = true;
        }

        public ValidationResult Validate(ValidationTestMessage message)
        {
            ValidateMessage = message;
            ValidateCalled = true;

            if (message.Int < 0)
            {
                return new ValidationResult("Must be non-negative.", nameof(message.Int));
            }

            return default;
        }

        public ValidationTestMessage HandleMessage { get; private set; }
        public bool HandleCalled { get; private set; }
        public ValidationTestMessage ValidateMessage { get; private set; }
        public bool ValidateCalled { get; private set; }
    }

    public class BuilderResultValidationTestMessageHandler
    {
        [Validate]
        public void Handle(ValidationTestMessage message)
        {
            HandleMessage = message;
            HandleCalled = true;
        }

        public ValidationResultsBuilder Validate(ValidationTestMessage message)
        {
            var validationResults = new ValidationResultsBuilder();
            ValidateMessage = message;
            ValidateCalled = true;

            if (string.IsNullOrWhiteSpace(message.String))
            {
                validationResults.AddValidationResult("Must not be null nor whitespace.", nameof(message.String));
            }

            if (message.Int < 0)
            {
                validationResults.AddValidationResult("Must be non-negative.", nameof(message.Int));
            }
            return validationResults;
        }

        public ValidationTestMessage HandleMessage { get; private set; }
        public bool HandleCalled { get; private set; }
        public ValidationTestMessage ValidateMessage { get; private set; }
        public bool ValidateCalled { get; private set; }
    }

    public class CombineBuilderResultValidationTestMessageHandler
    {
        public CombineBuilderResultValidationTestMessageHandler(bool returnNull, bool returnInjected)
        {
            ReturnNull = returnNull;
            ReturnInjected = returnInjected;
        }

        [Validate]
        public void Handle(ValidationTestMessage message)
        {
            HandleMessage = message;
            HandleCalled = true;
        }

        public ValidationResultsBuilder Validate(ValidationTestMessage message, ValidationResultsBuilder injected)
        {
            var validationResults = new ValidationResultsBuilder();
            ValidateMessage = message;
            ValidateCalled = true;

            if (string.IsNullOrWhiteSpace(message.String))
            {
                injected.AddValidationResult("Must not be null nor whitespace.", nameof(message.String));
            }

            if (message.Int < 0)
            {
                ((ReturnNull || ReturnInjected) ? injected : validationResults).AddValidationResult("Must be non-negative.", nameof(message.Int));
            }

            if (ReturnNull)
                return null;

            if (ReturnInjected)
                return injected;

            return validationResults;
        }

        public ValidationTestMessage HandleMessage { get; private set; }
        public bool HandleCalled { get; private set; }
        public ValidationTestMessage ValidateMessage { get; private set; }
        public bool ValidateCalled { get; private set; }
        public bool ReturnNull { get; }
        public bool ReturnInjected { get; }
    }
}
