using System.Runtime.InteropServices;

#if MONOGAME
using Microsoft.Xna.Framework;
#else
using System.Numerics;
#endif

namespace ImGuiNET
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DrawVert
    {
        public Vector2 pos;
        public Vector2 uv;
        public uint col;

        public const int PosOffset = 0;
        public const int UVOffset = 8;
        public const int ColOffset = 16;
    };
}
