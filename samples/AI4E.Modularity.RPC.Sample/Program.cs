using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Proxying;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;

namespace AI4E.Modularity.RPC.Sample
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            using (var fs1 = new FloatingStream())
            using (var fs2 = new FloatingStream())
            using (var mux1 = new MultiplexStream(fs1, fs2))
            using (var mux2 = new MultiplexStream(fs2, fs1))
            {
                var server = new ProxyHost(mux1, new ServiceCollection().BuildServiceProvider());

                Task.Run(async () =>
                {
                    var client = new ProxyHost(mux2, new ServiceCollection().BuildServiceProvider());

                    var valueProxy = new Proxy<Value>(new Value(12), ownsInstance: true);
                    var barProxy = await client.ActivateAsync<Bar>(ActivationMode.Create, cancellation: default);
                    var fooProxy = await barProxy.ExecuteAsync(bar => bar.GetFoo());

                    Console.WriteLine(await fooProxy.ExecuteAsync(foo => foo.Add(1, 2)));
                    Console.WriteLine(await fooProxy.ExecuteAsync(foo => foo.AddAsync(1, 2)));
                    Console.WriteLine(await fooProxy.ExecuteAsync(foo => foo.Get<decimal>()));
                    Console.WriteLine(await fooProxy.ExecuteAsync(foo => foo.ReadValueAsync(valueProxy)));
                    Console.WriteLine(await valueProxy.ExecuteAsync(v => v.GetValue()));
                });

                Console.ReadLine();
                //GC.Collect();
                //GC.WaitForPendingFinalizers();
            }

            Console.ReadLine();
        }
    }

    public sealed class Foo : IDisposable
    {
        public int Add(int a, int b)
        {
            return a + b;
        }

        public Task<int> AddAsync(int a, int b)
        {
            return Task.FromResult(a + b);
        }

        public T Get<T>()
        {
            return default;
        }

        public Task<int> ReadValueAsync(Proxy<Value> proxy)
        {
            return proxy.ExecuteAsync(value => value.GetValue());
        }

        public void Dispose()
        {
            Console.WriteLine("Destroying foo");
        }
    }

    public sealed class Bar : IDisposable
    {
        public Proxy<Foo> GetFoo()
        {
            return new Proxy<Foo>(new Foo(), ownsInstance: true);
        }

        public void Dispose()
        {
            Console.WriteLine("Destroying bar");
        }
    }

    public sealed class Value : IDisposable
    {
        private readonly int _value;

        public Value(int value)
        {
            _value = value;
        }

        public int GetValue()
        {
            return _value;
        }

        public void Dispose()
        {
            Console.WriteLine("Destroying value");
        }
    }

    public sealed class MultiplexStream : Stream
    {
        private readonly Stream _rx;
        private readonly Stream _tx;

        public MultiplexStream(Stream rx, Stream tx)
        {
            if (rx == null)
                throw new ArgumentNullException(nameof(rx));

            if (tx == null)
                throw new ArgumentNullException(nameof(tx));

            _rx = rx;
            _tx = tx;
        }

        public override bool CanRead => _rx.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => _rx.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            _tx.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _rx.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _tx.Write(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _rx.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _tx.WriteAsync(buffer, offset, count, cancellationToken);
        }
    }

    public sealed class FloatingStream : Stream
    {
        private readonly AsyncProducerConsumerQueue<ArraySegment<byte>> _queue = new AsyncProducerConsumerQueue<ArraySegment<byte>>();
        private readonly CancellationTokenSource _disposedCancellationSource = new CancellationTokenSource();
        private ArraySegment<byte> _current = new ArraySegment<byte>();

        public FloatingStream() { }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count == 0)
                return;

            if (buffer.Length - offset < count)
                throw new ArgumentException(); // TODO

            if (_disposedCancellationSource.IsCancellationRequested)
                throw new ObjectDisposedException(GetType().FullName);

            _queue.Enqueue(new ArraySegment<byte>(buffer, offset, count));
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (buffer.Length - offset < count)
                throw new ArgumentException(); // TODO

            if (_current.Array == null || _current.Count == 0)
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposedCancellationSource.Token);

                try
                {
                    _current = await _queue.DequeueAsync(cts.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }

            Debug.Assert(_current.Array != null);
            Debug.Assert(_current.Count > 0);

            var bytesToCopy = Math.Min(count, _current.Count);

            Array.Copy(_current.Array, _current.Offset, buffer, offset, bytesToCopy);

            if (_current.Count == bytesToCopy)
            {
                _current = default;
            }
            else
            {
                _current = new ArraySegment<byte>(_current.Array, _current.Offset + bytesToCopy, _current.Count - bytesToCopy);
            }

            return bytesToCopy;
        }

        protected override void Dispose(bool disposing)
        {
            _disposedCancellationSource.Cancel();
        }
    }
}
