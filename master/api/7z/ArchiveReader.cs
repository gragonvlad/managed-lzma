﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedLzma.SevenZip
{
    public sealed class DecodedSectionReader : IDisposable
    {
        #region Implementation

        private ArchiveMetadata mMetadata;
        private ArchiveDecoderSection mDecoderSection;
        private ArchiveSectionDecoder mDecodedStream;
        private DecodedStream mCurrentStream;
        private int mIndex;

        public DecodedSectionReader(Stream stream, ArchiveMetadata metadata, int index, PasswordStorage password)
        {
            mDecodedStream = new ArchiveSectionDecoder(stream, metadata, index, password);
            mMetadata = metadata;
            mDecoderSection = metadata.DecoderSections[index];
        }

        public void Dispose()
        {
            CloseCurrentStream();
            mDecodedStream.Dispose();
        }

        public int StreamCount
        {
            get { return mDecoderSection.Streams.Length; }
        }

        public int CurrentStreamIndex
        {
            get { return mIndex; }
        }

        public long CurrentStreamPosition
        {
            get { return mCurrentStream != null ? mCurrentStream.Position : 0; }
        }

        public long CurrentStreamLength
        {
            get { return mDecoderSection.Streams[mIndex].Length; }
        }

        public Checksum? CurrentStreamChecksum
        {
            get { return mDecoderSection.Streams[mIndex].Checksum; }
        }

        public Stream OpenStream()
        {
            if (this.CurrentStreamIndex == this.StreamCount)
                throw new InvalidOperationException("The reader contains no more streams.");

            if (mCurrentStream != null)
                throw new InvalidOperationException("Each stream can only be opened once.");

            mCurrentStream = new DecodedStream(mDecodedStream, this.CurrentStreamLength);

            return mCurrentStream;
        }

        public void NextStream()
        {
            if (this.CurrentStreamIndex == this.StreamCount)
                throw new InvalidOperationException("The reader contains no more streams.");

            var remaining = CurrentStreamLength - CurrentStreamPosition;

            CloseCurrentStream();

            while (remaining > Int32.MaxValue)
            {
                mDecodedStream.Skip(Int32.MaxValue);
                remaining -= Int32.MaxValue;
            }

            if (remaining > 0)
                mDecodedStream.Skip((int)remaining);

            mIndex++;
        }

        private void CloseCurrentStream()
        {
            if (mCurrentStream != null)
            {
                mCurrentStream.Dispose();
                mCurrentStream = null;
            }
        }

        #endregion
    }

    internal sealed class DecodedStream : Stream
    {
        #region Implementation

        private ArchiveSectionDecoder mReader;
        private long mOffset;
        private long mLength;

        internal DecodedStream(ArchiveSectionDecoder stream, long length)
        {
            mReader = stream;
            mLength = length;
        }

        protected override void Dispose(bool disposing)
        {
            // We mark the stream as disposed by clearing the base stream reference.
            // Note that we must keep the offset/length fields intact because our owner still needs them.
            mReader = null;

            base.Dispose(disposing);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        /// <remarks>
        /// Returning the length from a non-seekable stream is non-standard.
        /// The caller must know otherwise that we support this method.
        /// </remarks>
        public override long Length
        {
            get
            {
                return mLength;
            }
        }

        /// <remarks>
        /// Returning the position from a non-seekable stream is non-standard.
        /// The caller must know otherwise that we support this method.
        /// </remarks>
        public override long Position
        {
            get
            {
                return mOffset;
            }
            set
            {
                if (value < mOffset || value > mLength)
                    throw new ArgumentOutOfRangeException(nameof(value));

                Skip(value - mOffset);
            }
        }

        /// <summary>
        /// Seeking on a non-seekable stream is non-standard.
        /// The caller must know otherwise that we support this method.
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    if (offset < mOffset || offset > mLength)
                        throw new ArgumentOutOfRangeException(nameof(offset));

                    offset -= mOffset;
                    break;

                case SeekOrigin.Current:
                    if (offset < 0 || offset > mLength - mOffset)
                        throw new ArgumentOutOfRangeException(nameof(offset));

                    break;

                case SeekOrigin.End:
                    if (offset > 0 || offset < mOffset - mLength)
                        throw new ArgumentOutOfRangeException(nameof(offset));

                    offset += mLength;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }

            Skip(offset);
            return mOffset;
        }

        public void Skip(long offset)
        {
            if (offset < 0 || offset > mLength - mOffset)
                throw new ArgumentOutOfRangeException(nameof(offset));

            while (offset > Int32.MaxValue)
            {
                mReader.Skip(Int32.MaxValue);
                mOffset += Int32.MaxValue;
                offset -= Int32.MaxValue;
            }

            if (offset > 0)
            {
                mReader.Skip((int)offset);
                mOffset += offset;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0 || count > buffer.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (mReader == null)
                throw new ObjectDisposedException(null);

            var remaining = mLength - mOffset;
            if (count > remaining)
                count = (int)remaining;

            if (count == 0)
                return 0;

            var result = mReader.Read(buffer, offset, count);
            if (result <= 0 || result > count)
                throw new InternalFailureException();

            mOffset += result;
            return result;
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Flush()
        {
            throw new InvalidOperationException();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException();
        }

        #endregion
    }
}
