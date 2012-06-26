﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA.Master
{
    partial class LZMA
    {
        public struct SRes
        {
            private int _code;
            public SRes(int code) { _code = code; }
            public override int GetHashCode() { return _code; }
            public override bool Equals(object obj) { return obj is SRes && ((SRes)obj)._code == _code; }
            public bool Equals(SRes obj) { return obj._code == _code; }
            public static bool operator ==(SRes left, SRes right) { return left._code == right._code; }
            public static bool operator !=(SRes left, SRes right) { return left._code != right._code; }
            public static bool operator ==(SRes left, int right) { return left._code == right; }
            public static bool operator !=(SRes left, int right) { return left._code != right; }
        }

        public static SRes SZ_OK { get { return new SRes(0); } }

        public static SRes SZ_ERROR_DATA { get { return new SRes(1); } }
        public static SRes SZ_ERROR_MEM { get { return new SRes(2); } }
        public static SRes SZ_ERROR_CRC { get { return new SRes(3); } }
        public static SRes SZ_ERROR_UNSUPPORTED { get { return new SRes(4); } }
        public static SRes SZ_ERROR_PARAM { get { return new SRes(5); } }
        public static SRes SZ_ERROR_INPUT_EOF { get { return new SRes(6); } }
        public static SRes SZ_ERROR_OUTPUT_EOF { get { return new SRes(7); } }
        public static SRes SZ_ERROR_READ { get { return new SRes(8); } }
        public static SRes SZ_ERROR_WRITE { get { return new SRes(9); } }
        public static SRes SZ_ERROR_PROGRESS { get { return new SRes(10); } }
        public static SRes SZ_ERROR_FAIL { get { return new SRes(11); } }
        public static SRes SZ_ERROR_THREAD { get { return new SRes(12); } }

        public static SRes SZ_ERROR_ARCHIVE { get { return new SRes(16); } }
        public static SRes SZ_ERROR_NO_ARCHIVE { get { return new SRes(17); } }

        //#define RINOK(x) { int __result__ = (x); if (__result__ != 0) return __result__; }

        /* The following interfaces use first parameter as pointer to structure */

        /* if (input(*size) != 0 && output(*size) == 0) means end_of_stream.
           (output(*size) < input(*size)) is allowed */
        public interface ISeqInStream
        {
            SRes Read(P<byte> buf, ref long size);
        }

        public class CSeqInStream: ISeqInStream
        {
            private Func<P<byte>, long, long> mCallback;

            public CSeqInStream(Func<P<byte>, long, long> callback)
            {
                mCallback = callback;
            }

            SRes ISeqInStream.Read(P<byte> buf, ref long size)
            {
                try { size = mCallback(buf, size); }
                catch { return SZ_ERROR_READ; }
                return SZ_OK;
            }
        }

        /* Returns: result - the number of actually written bytes.
           (result < size) means error */
        public interface ISeqOutStream
        {
            long Write(P<byte> buf, long size);
        }

        public class CSeqOutStream: ISeqOutStream
        {
            private Action<P<byte>, long> mCallback;

            public CSeqOutStream(Action<P<byte>, long> callback)
            {
                mCallback = callback;
            }

            long ISeqOutStream.Write(P<byte> buf, long size)
            {
                if(size <= 0)
                {
                    System.Diagnostics.Debugger.Break();
                    return -1;
                }

                try { mCallback(buf, size); }
                catch { return 0; }
                return size;
            }
        }

        /* Returns: result. (result != SZ_OK) means break.
           Value (ulong)(long)-1 for size means unknown value. */
        public interface ICompressProgress
        {
            SRes Progress(ulong inSize, ulong outSize);
        }

        //public delegate object ISzAlloc_Alloc(object p, long size);
        //public delegate void ISzAlloc_Free(object p, object address); /* address can be null */
        public sealed class ISzAlloc
        {
            public static readonly ISzAlloc BigAlloc = new ISzAlloc(200);
            public static readonly ISzAlloc SmallAlloc = new ISzAlloc(100);

            private int mKind;

            private ISzAlloc(int kind)
            {
                mKind = kind;
            }

            public T Alloc<T>(object p)
                where T: new()
            {
                return new T();
            }

            public T[] Alloc<T>(object p, long size)
            {
                return new T[size];
            }

            public void Free(object p, object address)
            {
                // ignore
            }
        }

        public static object IAlloc_Alloc<T>(object p, long size)
        {
            return ((ISzAlloc)p).Alloc<T>(p, size);
        }

        public static void IAlloc_Free(object p, object a)
        {
            ((ISzAlloc)p).Free(p, a);
        }
    }
}