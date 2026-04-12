using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ZuneDeploy.Native;

static internal partial class MTP
{
    [LibraryImport("libzune-deploy-native.so")]
    internal static partial int OpenConnection(out IntPtr device);

    [LibraryImport("libzune-deploy-native.so")]

    internal static partial void CloseConnection(IntPtr device);

    [LibraryImport("libzune-deploy-native.so")]
    internal static partial int PollData(IntPtr device, [MarshalUsing(CountElementName = "size")] byte[] buffer, int size, out int bytesRead);

    [LibraryImport("libzune-deploy-native.so")]
    internal static partial int SendData(IntPtr device, [MarshalUsing(CountElementName = "size")] in byte[] buffer, int size);
}
