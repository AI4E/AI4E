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

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Proxying.Test.TestTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Utils.Proxying.Test
{
    [TestClass]
    public class ProxyHostTests
    {
        [TestMethod]
        public async Task SyncCallWithResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                var result = await fooProxy.ExecuteAsync(foo => foo.Add(5, 3));

                Assert.IsNull(fooProxy.LocalInstance);
                Assert.AreEqual(8, result);
            }
        }

        [TestMethod]
        public async Task AsyncCallWithResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                var result = await fooProxy.ExecuteAsync(foo => foo.AddAsync(5, 3));

                Assert.IsNull(fooProxy.LocalInstance);
                Assert.AreEqual(8, result);
            }
        }

        [TestMethod]
        public async Task SyncCallWithoutResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                await fooProxy.ExecuteAsync(foo => foo.Set(5));

                Assert.IsNull(fooProxy.LocalInstance);
                Assert.AreEqual(5, ((IFoo)remoteProxyHost.LocalProxies.First().LocalInstance).Get());
            }
        }

        [TestMethod]
        public async Task AsyncCallWithoutResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                await fooProxy.ExecuteAsync(foo => foo.SetAsync(5));

                Assert.IsNull(fooProxy.LocalInstance);
                Assert.AreEqual(5, ((IFoo)remoteProxyHost.LocalProxies.First().LocalInstance).Get());
            }
        }

        [TestMethod]
        public async Task TransparentProxySyncCallWithResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var foo = (await localProxyHost.CreateAsync<Foo>(cancellation: default)).Cast<IFoo>().AsTransparentProxy();
                var result = foo.Add(5, 3);

                Assert.AreEqual(8, result);
            }
        }

        [TestMethod]
        public async Task TransparentProxyAsyncCallWithResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var foo = (await localProxyHost.CreateAsync<Foo>(cancellation: default)).Cast<IFoo>().AsTransparentProxy();
                var result = await foo.AddAsync(5, 3);

                Assert.AreEqual(8, result);
            }
        }

        [TestMethod]
        public async Task TransparentProxySyncCallWithoutResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var foo = (await localProxyHost.CreateAsync<Foo>(cancellation: default)).Cast<IFoo>().AsTransparentProxy();
                foo.Set(5);

                await Task.Delay(50);

                Assert.AreEqual(5, ((IFoo)remoteProxyHost.LocalProxies.First().LocalInstance).Get());
            }
        }

        [TestMethod]
        public async Task TransparentProxyAsyncCallWithoutResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var foo = (await localProxyHost.CreateAsync<Foo>(cancellation: default)).Cast<IFoo>().AsTransparentProxy();
                await foo.SetAsync(5);

                Assert.AreEqual(5, ((IFoo)remoteProxyHost.LocalProxies.First().LocalInstance).Get());
            }
        }

        [TestMethod]
        public void LocalProxyAsTransparentProxyTest()
        {
            var instance = new Foo();
            var transparentProxy = ProxyHost.CreateProxy(instance).Cast<IFoo>().AsTransparentProxy();

            Assert.AreSame(instance, ((IProxyInternal)transparentProxy).LocalInstance);
        }

        [TestMethod]
        public void NonInterfaceTransparentProxyTest()
        {
            var instance = new Foo();

            Assert.ThrowsException<NotSupportedException>(() =>
            {
                ProxyHost.CreateProxy(instance).AsTransparentProxy();
            });
        }

        [TestMethod]
        public async Task RemoteProxyComplianceTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var localProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                var remoteProxy = remoteProxyHost.LocalProxies.FirstOrDefault();

                Assert.IsNotNull(remoteProxy);
                Assert.IsInstanceOfType(remoteProxy, typeof(IProxy<Foo>));
                Assert.IsNotNull(remoteProxy.LocalInstance);
                Assert.IsInstanceOfType(remoteProxy.LocalInstance, typeof(Foo));
                Assert.AreEqual(((IProxyInternal)localProxy).Id, remoteProxy.Id);
            }
        }

        [TestMethod]
        public async Task ProxyDisposalTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var localProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                var remoteProxy = (IProxy<Foo>)remoteProxyHost.LocalProxies.First();

                await localProxy.DisposeAsync();

                await Task.Delay(50);

                GC.Collect();
                GC.WaitForPendingFinalizers();

                // The remote proxy must be unregistered.
                Assert.IsFalse(remoteProxyHost.LocalProxies.Contains((IProxyInternal)remoteProxy));

                // The remote proxy must be disposed.
                Assert.IsTrue(remoteProxy.Disposal.Status == TaskStatus.RanToCompletion);

                // The remote proxy value must be disposed.
                Assert.IsTrue(remoteProxy.LocalInstance.IsDisposed);

                // The local proxy must be disposed.
                Assert.IsTrue(localProxy.Disposal.Status == TaskStatus.RanToCompletion);
            }
        }

        [TestMethod]
        public async Task LocalProxyHostDisposalTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var localProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                var remoteProxy = (IProxy<Foo>)remoteProxyHost.LocalProxies.First();

                await localProxyHost.DisposeAsync();

                await Task.Delay(50);

                GC.Collect();
                GC.WaitForPendingFinalizers();

                // The remote proxy must be unregistered.
                Assert.IsFalse(remoteProxyHost.LocalProxies.Contains((IProxyInternal)remoteProxy));

                // The remote proxy must be disposed.
                Assert.IsTrue(remoteProxy.Disposal.Status == TaskStatus.RanToCompletion);

                // The remote proxy value must be disposed.
                Assert.IsTrue(remoteProxy.LocalInstance.IsDisposed);

                // The local proxy must be disposed.
                Assert.IsTrue(localProxy.Disposal.Status == TaskStatus.RanToCompletion);
            }
        }

        [TestMethod]
        public async Task RemoteProxyHostDisposalTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var localProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                var remoteProxy = (IProxy<Foo>)remoteProxyHost.LocalProxies.First();

                await remoteProxyHost.DisposeAsync();

                await Task.Delay(50);

                GC.Collect();
                GC.WaitForPendingFinalizers();

                // The remote proxy must be unregistered.
                Assert.IsFalse(remoteProxyHost.LocalProxies.Contains((IProxyInternal)remoteProxy));

                // The remote proxy must be disposed.
                Assert.IsTrue(remoteProxy.Disposal.Status == TaskStatus.RanToCompletion);

                // The remote proxy value must be disposed.
                Assert.IsTrue(remoteProxy.LocalInstance.IsDisposed);

                // The local proxy must be disposed.
                Assert.IsTrue(localProxy.Disposal.Status == TaskStatus.RanToCompletion);
            }
        }

        [TestMethod]
        public async Task ConnectionBreakdownTest()
        {
            ProxyHost localProxyHost, remoteProxyHost;
            IProxy<Foo> localProxy, remoteProxy;

            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                localProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                remoteProxy = (IProxy<Foo>)remoteProxyHost.LocalProxies.First();
            }

            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () =>
            {
                await localProxy.ExecuteAsync(foo => foo.Add(1, 1));
            });

            await Task.Delay(50);

            // The remote proxy must be unregistered.
            Assert.IsFalse(remoteProxyHost.LocalProxies.Contains((IProxyInternal)remoteProxy));

            // The remote proxy must be disposed.
            Assert.IsTrue(remoteProxy.Disposal.Status == TaskStatus.RanToCompletion);

            // The remote proxy value must be disposed.
            Assert.IsTrue(remoteProxy.LocalInstance.IsDisposed);

            // The local proxy must be disposed.
            Assert.IsTrue(localProxy.Disposal.Status == TaskStatus.RanToCompletion);
        }

        // TODO: Do we need to test this for all of the 4 ExecuteAsync methods? (YES)
        [TestMethod]
        public async Task MethodExecutionWhenDisposedTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var localProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);

                await localProxy.DisposeAsync();

                GC.Collect();
                GC.WaitForPendingFinalizers();

                await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () =>
                {
                    await localProxy.ExecuteAsync(foo => foo.Add(1, 1));
                });
            }
        }

        [TestMethod]
        public async Task ResolveFromServicesTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider(services =>
                {
                    services.AddSingleton<Value>(_ => new Value(10));
                }));

                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var valueProxy = await localProxyHost.LoadAsync<Value>(cancellation: default);
                var result = await valueProxy.ExecuteAsync(value => value.GetValue());

                Assert.AreEqual(10, result);

                await valueProxy.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task RemoteCreateProxyTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var barProxy = await localProxyHost.CreateAsync<Bar>(cancellation: default);
                var fooProxy = await barProxy.ExecuteAsync(bar => bar.GetFoo());

                Assert.IsNull(fooProxy.LocalInstance);

                var result = await fooProxy.ExecuteAsync(value => value.Add(10, 5));

                Assert.AreEqual(15, result);

                await fooProxy.DisposeAsync();
                await barProxy.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task LocalProxyRoundtripTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                var value = new Value(5);
                var valueLocalProxy = ProxyHost.CreateProxy<Value>(value, ownsInstance: true);

                var resultProxy = await fooProxy.ExecuteAsync(foo => foo.GetBackProxy(valueLocalProxy));

                Assert.IsNotNull(resultProxy.LocalInstance);
                Assert.IsInstanceOfType(resultProxy, typeof(IProxy<Value>));
                Assert.IsNotNull(resultProxy.LocalInstance);
                Assert.IsInstanceOfType(resultProxy.LocalInstance, typeof(Value));
                Assert.AreSame(value, resultProxy.LocalInstance);
                Assert.AreEqual(((IProxyInternal)valueLocalProxy).Id, ((IProxyInternal)resultProxy).Id);

                // TODO: Do we really need to enforce this?
                Assert.AreSame(valueLocalProxy, resultProxy);
            }
        }

        [TestMethod]
        public async Task LocalToRemoteReverseProxyTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                var value = new Value(5);
                var valueLocalProxy = ProxyHost.CreateProxy<Value>(value, ownsInstance: true);

                var result = await fooProxy.ExecuteAsync(foo => foo.ReadValueAsync(valueLocalProxy));

                Assert.AreEqual(5, result);
            }
        }

        [TestMethod]
        public async Task CancellationTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var tcs = new TaskCompletionSource<object>();
                var cancellationTestType = new CancellationTestType { TaskCompletionSource = tcs };

                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider(services =>
                {
                    services.AddSingleton(cancellationTestType);
                }));
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.LoadAsync<CancellationTestType>(cancellation: default);

                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var task = proxy.ExecuteAsync(t => t.OperateAsync(26, cancellationTokenSource.Token));
                    await Task.Delay(50);

                    Assert.IsTrue(cancellationTestType.Cancellation.CanBeCanceled);
                    Assert.IsFalse(cancellationTestType.Cancellation.IsCancellationRequested);

                    cancellationTokenSource.Cancel();

                    await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => task);

                    Assert.IsTrue(cancellationTestType.Cancellation.CanBeCanceled);
                    Assert.IsTrue(cancellationTestType.Cancellation.IsCancellationRequested);
                }
            }
        }

        [TestMethod]
        public async Task DowncastLocalProxyTest()
        {
            var instance = new Foo();
            var proxy = ProxyHost.CreateProxy(instance);
            var castProxy = (CastProxy<Foo, object>)proxy.Cast<object>();

            Assert.AreSame(proxy, castProxy.Original);
            Assert.AreEqual(((IProxyInternal)proxy).Id, castProxy.Id);
            Assert.AreEqual(await proxy.GetObjectTypeAsync(), await castProxy.GetObjectTypeAsync(default));
            Assert.AreSame(proxy.LocalInstance, castProxy.LocalInstance);
            Assert.AreEqual(typeof(object), castProxy.RemoteType);
        }

        [TestMethod]
        public void UpcastLocalProxyTest()
        {
            var instance = new Foo();
            var proxy = ProxyHost.CreateProxy<object>(instance);

            Assert.ThrowsException<ArgumentException>(() =>
            {
                proxy.Cast<Foo>();
            });
        }

        [TestMethod]
        public async Task DowncastRemoteProxyTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);

                var castProxy = (CastProxy<Foo, object>)proxy.Cast<object>();

                Assert.AreSame(proxy, castProxy.Original);
                Assert.AreEqual(((IProxyInternal)proxy).Id, castProxy.Id);
                Assert.AreEqual(await proxy.GetObjectTypeAsync(default), await castProxy.GetObjectTypeAsync(default));
                Assert.IsNull(castProxy.LocalInstance);
                Assert.AreEqual(typeof(object), castProxy.RemoteType);
            }
        }

        [TestMethod]
        public async Task UpcastRemoteProxyTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider(services =>
                {
                    services.AddTransient<IFoo, Foo>();
                }));

                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.LoadAsync<IFoo>(cancellation: default);

                Assert.ThrowsException<ArgumentException>(() =>
                {
                    proxy.Cast<Foo>();
                });
            }
        }

        [TestMethod]
        public void InvalidUpcastLocalProxyTest()
        {
            var instance = new Foo();
            var proxy = ProxyHost.CreateProxy<object>(instance);

            Assert.ThrowsException<ArgumentException>(() =>
            {
                proxy.Cast<Bar>();
            });
        }

        [TestMethod]
        public async Task CastProxyAgainTest()
        {
            var instance = new Foo();
            var proxy = ProxyHost.CreateProxy(instance);
            var castProxy = proxy.Cast<IFoo>();
            var castAgainProxy = (CastProxy<Foo, object>)castProxy.Cast<object>();

            Assert.AreSame(proxy, castAgainProxy.Original);
            Assert.AreEqual(((IProxyInternal)proxy).Id, castAgainProxy.Id);
            Assert.AreEqual(await proxy.GetObjectTypeAsync(default), await castAgainProxy.GetObjectTypeAsync(default));
            Assert.AreSame(proxy.LocalInstance, castAgainProxy.LocalInstance);
            Assert.AreEqual(typeof(object), castAgainProxy.RemoteType);
        }

        [TestMethod]
        public async Task CastProxySyncCallWithResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = (await localProxyHost.CreateAsync<Foo>(cancellation: default)).Cast<IFoo>();
                var result = await fooProxy.ExecuteAsync(foo => foo.Add(5, 3));

                Assert.IsNull(fooProxy.LocalInstance);
                Assert.AreEqual(8, result);
            }
        }

        [TestMethod]
        public async Task CastProxyAsyncCallWithResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = (await localProxyHost.CreateAsync<Foo>(cancellation: default)).Cast<IFoo>();
                var result = await fooProxy.ExecuteAsync(foo => foo.AddAsync(5, 3));

                Assert.IsNull(fooProxy.LocalInstance);
                Assert.AreEqual(8, result);
            }
        }

        [TestMethod]
        public async Task CastProxySyncCallWithoutResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = (await localProxyHost.CreateAsync<Foo>(cancellation: default)).Cast<IFoo>();
                await fooProxy.ExecuteAsync(foo => foo.Set(5));

                Assert.IsNull(fooProxy.LocalInstance);
                Assert.AreEqual(5, ((IFoo)remoteProxyHost.LocalProxies.First().LocalInstance).Get());
            }
        }

        [TestMethod]
        public async Task CastProxyAsyncCallWithoutResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = (await localProxyHost.CreateAsync<Foo>(cancellation: default)).Cast<IFoo>();
                await fooProxy.ExecuteAsync(foo => foo.SetAsync(5));

                Assert.IsNull(fooProxy.LocalInstance);
                Assert.AreEqual(5, ((IFoo)remoteProxyHost.LocalProxies.First().LocalInstance).Get());
            }
        }

        [TestMethod]
        public async Task RemoteCreateTransparentProxyTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var barProxy = await localProxyHost.CreateAsync<Bar>(cancellation: default);
                var fooTransparentProxy = await barProxy.ExecuteAsync(bar => bar.GetFooTransparent());

                Assert.IsNotNull(fooTransparentProxy);

                var result = fooTransparentProxy.Add(14, 5);

                Assert.AreEqual(19, result);
            }
        }

        [TestMethod]
        public async Task LocalToRemoteTransparentReverseProxyTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                var value = new Value(7);
                var transparentProxy = ProxyHost.CreateProxy(value).Cast<IValue>().AsTransparentProxy();
                var result = await fooProxy.ExecuteAsync(foo => foo.ReadValueAsync(transparentProxy));

                Assert.AreEqual(7, result);
            }
        }

        [TestMethod]
        public async Task LocalProxyTransparentProxyRoundtripTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = await localProxyHost.CreateAsync<Foo>(cancellation: default);
                var value = new Value(5);
                var valueLocalProxy = ProxyHost.CreateProxy<Value>(value, ownsInstance: true);

                var transparentProxy = await fooProxy.ExecuteAsync(foo => foo.GetBackTransparentProxy(valueLocalProxy));

                Assert.AreSame(value, transparentProxy);
            }
        }

        [TestMethod]
        public async Task ComplexTypeRountripTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.CreateAsync<ComplexTypeStub>(cancellation: default);
                var complexType = new ComplexType { Int = 5, Str = "Test" };
                var resultComplexType = await proxy.ExecuteAsync(p => p.Echo(complexType));

                Assert.IsNotNull(resultComplexType);
                Assert.AreEqual(complexType.Int, resultComplexType.Int);
                Assert.AreEqual(complexType.Str, resultComplexType.Str);
            }
        }

        [TestMethod]
        public async Task ComplexTypeWithProxyTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.CreateAsync<ComplexTypeStub>(cancellation: default);
                var value = new Value(10);
                var complexType = new ComplexTypeWithProxy
                {
                    ProxyName = "MyProxy",
                    Proxy = ProxyHost.CreateProxy(value)
                };
                var result = await proxy.ExecuteAsync(p => p.GetValueAsync(complexType));

                Assert.AreEqual(value.GetValue(), result);
            }
        }

        [TestMethod]
        public async Task ComplexTypeWithTransparentProxyTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.CreateAsync<ComplexTypeStub>(cancellation: default);
                var value = new Value(10);
                var complexType = new ComplexTypeWithTransparentProxy
                {
                    ProxyName = "MyProxy",
                    Proxy = ProxyHost.CreateProxy(value).Cast<IValue>().AsTransparentProxy()
                };
                var result = await proxy.ExecuteAsync(p => p.GetValue(complexType));

                Assert.AreEqual(value.GetValue(), result);
            }
        }

        [TestMethod]
        public async Task ComplexTypeWithCancellationTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var tcs = new TaskCompletionSource<object>();
                var cancellationTestType = new ComplexTypeStub { TaskCompletionSource = tcs };

                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider(services =>
                {
                    services.AddSingleton(cancellationTestType);
                }));
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.LoadAsync<ComplexTypeStub>(cancellation: default);

                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var complexType = new ComplexTypeWithCancellationToken
                    {
                        Str = "abc",
                        CancellationToken = cancellationTokenSource.Token
                    };

                    var task = proxy.ExecuteAsync(t => t.OperateAsync(complexType));
                    await Task.Delay(50);

                    Assert.IsTrue(cancellationTestType.Cancellation.CanBeCanceled);
                    Assert.IsFalse(cancellationTestType.Cancellation.IsCancellationRequested);

                    cancellationTokenSource.Cancel();

                    await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => task);

                    Assert.IsTrue(cancellationTestType.Cancellation.CanBeCanceled);
                    Assert.IsTrue(cancellationTestType.Cancellation.IsCancellationRequested);
                }
            }
        }

        [TestMethod]
        public async Task ComplexTypeWithProxyResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.CreateAsync<ComplexTypeStub>(cancellation: default);
                var complexType = await proxy.ExecuteAsync(p => p.GetComplexTypeWithProxy());
                var result = await complexType.Proxy.ExecuteAsync(p => p.GetValue());

                Assert.AreEqual(23, result);
            }
        }

        [TestMethod]
        public async Task ComplexTypeWithTransparentProxyResultTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.CreateAsync<ComplexTypeStub>(cancellation: default);
                var complexType = await proxy.ExecuteAsync(p => p.GetComplexTypeWithTransparentProxy());
                var result = complexType.Proxy.GetValue();

                Assert.AreEqual(23, result);
            }
        }

        [TestMethod]
        public async Task NonDuplicatedProxyOnBackReferenceTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.CreateAsync<ComplexTypeStub>(cancellation: default);
                var complexType = await proxy.ExecuteAsync(p => p.GetComplexObjectWithBackReference());

                Assert.AreSame(proxy, complexType.Proxy);
            }
        }

        [TestMethod]
        public async Task NonDuplicatedProxyOnTransparentBackReferenceTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider(services => services.AddTransient<IComplexTypeStub, ComplexTypeStub>()));
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.LoadAsync<IComplexTypeStub>(cancellation: default);
                var complexType = await proxy.ExecuteAsync(p => p.GetComplexObjectWithTransparentBackReference());
                var result = complexType.Proxy;

                Assert.IsInstanceOfType(result, typeof(TransparentProxy<IComplexTypeStub>));
                Assert.AreSame(proxy, (result as TransparentProxy<IComplexTypeStub>).Proxy);
            }
        }

        [TestMethod]
        public async Task SyncActivationTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider());
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = localProxyHost.Create<Foo>();
                var result = await fooProxy.ExecuteAsync(foo => foo.Add(5, 3));

                Assert.AreEqual(8, result);
                Assert.IsTrue(((IProxyInternal)fooProxy).IsActivated);
            }
        }

        [TestMethod]
        public async Task SyncActivatedProxyInArgumentsTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider(services => services.AddSingleton(new Value(5))));
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var fooProxy = await localProxyHost.CreateAsync<Foo>(default);
                var valueProxy = localProxyHost.Load<Value>();

                var result = await fooProxy.ExecuteAsync(foo => foo.ReadValueAsync(valueProxy));

                Assert.AreEqual(5, result);
                Assert.IsTrue(((IProxyInternal)valueProxy).IsActivated);
            }
        }

        [TestMethod]
        public async Task SyncActivatedProxyInComlexTypeTest()
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var remoteProxyHost = new ProxyHost(mux1, BuildServiceProvider(services => services.AddSingleton(new Value(5))));
                var localProxyHost = new ProxyHost(mux2, BuildServiceProvider());

                var proxy = await localProxyHost.CreateAsync<ComplexTypeStub>(default);
                var valueProxy = localProxyHost.Load<Value>();

                var result = await proxy.ExecuteAsync(p => p.GetValueAsync(new ComplexTypeWithProxy { Proxy = valueProxy }));

                Assert.AreEqual(5, result);
                Assert.IsTrue(((IProxyInternal)valueProxy).IsActivated);
            }
        }

        // TODO: Add tests for all primitive types and properties.

        private IServiceProvider BuildServiceProvider()
        {
            return new ServiceCollection().BuildServiceProvider();
        }

        private IServiceProvider BuildServiceProvider(Action<IServiceCollection> servicesBuilder)
        {
            var serviceCollection = new ServiceCollection();
            servicesBuilder(serviceCollection);
            return serviceCollection.BuildServiceProvider();
        }
    }
}
