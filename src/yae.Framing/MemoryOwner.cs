﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace yae.Framing
{
    //credits: https://github.com/mgravell/simplsockets/blob/master/SimplPipelines/MemoryOwner.cs
    public static class MemoryOwner
    {
        public static int LeakCount<T>() => ArrayPoolOwner<T>.LeakCount();
        public static IMemoryOwner<T> Empty<T>() => SimpleMemoryOwner<T>.Empty;

        public static IMemoryOwner<T> Owned<T>(this Memory<T> memory)
            => new SimpleMemoryOwner<T>(memory);

        /// <summary>
        /// Creates a lease over the provided array; the contents are not copied - the array
        /// provided will be handed to the pool when disposed
        /// </summary>
        public static IMemoryOwner<T> Lease<T>(this T[] source, int length = -1)
        {
            if (source == null) return null; // GIGO
            if (length < 0) length = source.Length;
            else if (length > source.Length) throw new ArgumentOutOfRangeException(nameof(length));
            return length == 0 ? Empty<T>() : new ArrayPoolOwner<T>(source, length);
        }
        /// <summary>
        /// Creates a lease from the provided sequence, copying the data out into a linear vector
        /// </summary>
        public static IMemoryOwner<T> Lease<T>(this ReadOnlySequence<T> source)
        {
            if (source.IsEmpty) return Empty<T>();

            int len = checked((int)source.Length);
            var arr = ArrayPool<T>.Shared.Rent(len);
            source.CopyTo(arr);
            return new ArrayPoolOwner<T>(arr, len);
        }

        private sealed class SimpleMemoryOwner<T> : IMemoryOwner<T>
        {
            public static IMemoryOwner<T> Empty { get; } = new SimpleMemoryOwner<T>(Array.Empty<T>());
            public SimpleMemoryOwner(Memory<T> memory) => Memory = memory;

            public Memory<T> Memory { get; }
            public void Dispose() { }
        }

        /// <summary>
        /// A thin wrapper around a leased array; when disposed, the array
        /// is returned to the pool; the caller is responsible for not retaining
        /// a reference to the array (via .Memory / .ArraySegment) after using Dispose()
        /// </summary>
        private sealed class ArrayPoolOwner<T> : IMemoryOwner<T>
        {
            private readonly int _length;
            private T[] _oversized;

            internal ArrayPoolOwner(T[] oversized, int length)
            {
                _length = length;
                _oversized = oversized;
            }

            public Memory<T> Memory => new Memory<T>(GetArray(), 0, _length);

            private T[] GetArray() =>
                Interlocked.CompareExchange(ref _oversized, null, null)
                ?? throw new ObjectDisposedException(ToString());

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                var arr = Interlocked.Exchange(ref _oversized, null);
                if (arr != null) ArrayPool<T>.Shared.Return(arr);
            }

            ~ArrayPoolOwner() { Interlocked.Increment(ref _leakCount); }
            private static int _leakCount;
            internal static int LeakCount() => Thread.VolatileRead(ref _leakCount);
        }
    }
}
