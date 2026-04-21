using System;
using System.Runtime.InteropServices;

namespace PDFiumSharp.Types
{ // ReSharper disable once InconsistentNaming
    [StructLayout(LayoutKind.Sequential)]
    public struct FX_DOWNLOADHINTS
    {
        public readonly int Version;
        public readonly IntPtr AddSegment;
    }
}