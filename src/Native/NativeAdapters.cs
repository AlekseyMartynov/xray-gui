using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.NetworkManagement.Ndis;

namespace Project;

readonly ref struct NativeAdapterInfo {
    public required ReadOnlySpan<char> Name { get; init; }
    public required ReadOnlySpan<char> Description { get; init; }
    public required IF_OPER_STATUS Status { get; init; }
    public required uint IPv4Index { get; init; }
    public required uint IPv6Index { get; init; }
    public required bool IPv4Enabled { get; init; }
    public required bool IPv6Enabled { get; init; }
    public required ReadOnlySpan<NativeIPAddress> Unicast { get; init; }
}

static class NativeAdapters {

    public static unsafe void Enumerate(Action<NativeAdapterInfo> callback) {
        var family = (uint)ADDRESS_FAMILY.AF_UNSPEC;

        var flags = GET_ADAPTERS_ADDRESSES_FLAGS.GAA_FLAG_SKIP_ANYCAST
            | GET_ADAPTERS_ADDRESSES_FLAGS.GAA_FLAG_SKIP_MULTICAST
            | GET_ADAPTERS_ADDRESSES_FLAGS.GAA_FLAG_SKIP_DNS_SERVER;

        uint bufSize = 0;
        PInvoke.GetAdaptersAddresses(family, flags, null, ref bufSize);

        var buf = Marshal.AllocHGlobal((int)bufSize);
        try {
            var unicastList = new List<NativeIPAddress>();
            var itemPtr = (IP_ADAPTER_ADDRESSES_LH*)buf;
            NativeUtils.MustSucceed(PInvoke.GetAdaptersAddresses(family, flags, itemPtr, ref bufSize));
            while(itemPtr != null) {
                unicastList.Clear();
                var unicastPtr = itemPtr->FirstUnicastAddress;
                while(unicastPtr != null) {
                    unicastList.Add(NativeIPAddress.From(unicastPtr->Address.lpSockaddr));
                    unicastPtr = unicastPtr->Next;
                }
                callback(new NativeAdapterInfo {
                    Name = itemPtr->FriendlyName.AsSpan(),
                    Description = itemPtr->Description.AsSpan(),
                    Status = itemPtr->OperStatus,
                    IPv4Index = itemPtr->Anonymous1.Anonymous.IfIndex,
                    IPv6Index = itemPtr->Ipv6IfIndex,
                    IPv4Enabled = itemPtr->Anonymous2.Anonymous.Ipv4Enabled,
                    IPv6Enabled = itemPtr->Anonymous2.Anonymous.Ipv6Enabled,
                    Unicast = CollectionsMarshal.AsSpan(unicastList),
                });
                itemPtr = itemPtr->Next;
            }
        } finally {
            Marshal.FreeHGlobal(buf);
        }
    }
}
