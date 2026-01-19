using System.Runtime.InteropServices;

namespace DCTravelerX.Infos;

[StructLayout(LayoutKind.Explicit, Size = 0x158)]
public struct LobbyUIClientExposed
{
    [FieldOffset(0x18)]
    public nint Context;

    [FieldOffset(0x158)]
    public byte State;
}
