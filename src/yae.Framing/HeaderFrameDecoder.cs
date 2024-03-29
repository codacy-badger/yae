﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;

namespace yae.Framing
{
    public abstract class HeaderFrameDecoder<TFrame> : PipeFrameDecoder<TFrame>
        where TFrame : IFrame
    {
        protected HeaderFrameDecoder(PipeReader reader) : base(reader)
        {
        }

        public override bool TryParseFrame(SequenceReader<byte> reader, out TFrame frame, 
            out SequencePosition consumedTo)
        {
            var canParseHeader = TryParseHeader(ref reader, out frame, out var length);
            var canParsePayload = reader.Remaining >= length;

            if(length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (canParseHeader && canParsePayload)
            {
                var payload = reader.Sequence.Slice(reader.Position, length);
                frame.Payload = payload.Lease();

                reader.Advance(length);
                consumedTo = reader.Position;
                return true;
            }

            consumedTo = default;
            return false;
        }

        /// <summary>
        /// Try to parses the header of the frame.
        /// Frame must be returned only if you have one.
        /// Length is the payload length, you must exclude the header length.
        /// If you have nothing, just return default.
        /// </summary>
        /// <param name="sequenceReader">data</param>
        /// <param name="frame">returns frame only if you have one, otherwise returns default.</param>
        /// <param name="length">returns the length of the frame, excluding the header.</param>
        /// <returns></returns>
        public abstract bool TryParseHeader(ref SequenceReader<byte> sequenceReader, out TFrame frame, out int length);
    }
}