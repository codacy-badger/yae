﻿using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace yae.Framing.Tests
{
    class Frame
    {
        public Memory<byte> Data { get; set; }
    }
    class Encoder : IFrameEncoder<Frame>
    {
        public int GetHeaderLength(Frame frame) => 4;

        public ReadOnlyMemory<byte> GetPayload(Frame frame)
        {
            return frame.Data;
        }

        public void WriteHeader(Span<byte> span, Frame frame)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span, frame.Data.Length); //writes length
        }
    }

    class TestPipeFrameProducer : PipeFrameProducer<Frame>
    {
        protected override ValueTask<FlushResult> WriteAsync(PipeWriter writer, Frame frame)
        {
            return writer.WriteAsync(frame.Data);
        }

        public TestPipeFrameProducer(PipeWriter writer) : base(writer)
        {

        }
    }

    public class PipeProducerFrameTests
    {
        [Fact]
        public void ValueTaskDefault()
        {
            ValueTask<FlushResult> result = default;
            Assert.True(result.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task SlowPath()
        {
            var (producer, _) = GetProducer();
            await producer._semaphore.WaitAsync();
            //let's write and release
            var produceTask = producer.ProduceAsync(GetFrame());
            producer._semaphore.Release(); //released...
            var result = await produceTask;
            Assert.True(produceTask.IsCompleted);
            Assert.Equal(-1, result); //-1 means slowPath
        }

        private Frame GetFrame()
        {
            return new Frame {Data = new byte[256]};
        }
        private (TestPipeFrameProducer producer, PipeReader reader) GetProducer()
        {
            var pipe = new Pipe();
            return (new TestPipeFrameProducer(pipe.Writer), pipe.Reader);

        }
    }
}
