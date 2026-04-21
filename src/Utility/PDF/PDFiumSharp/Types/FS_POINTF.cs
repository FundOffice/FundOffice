using System.Runtime.InteropServices;

namespace PDFiumSharp.Types
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FS_POINTF
    {
        public float X { get; }
        public float Y { get; }

        public FS_POINTF(float x, float y)
        {
            this.X = x;
            this.Y = y;
        }
    }
}