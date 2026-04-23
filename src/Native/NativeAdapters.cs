using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.NetworkManagement.Ndis;

namespace Project;

readonly ref struct NativeAdapterInfo {
    public required Guid Guid { get; init; }
    public required NET_LUID_LH Luid { get; init; }
    public required ReadOnlySpan<char> Name { get; init; }
    public required ReadOnlySpan<char> Description { get; init; }
    public required IF_OPER_STATUS Status { get; init; }
    public required bool IPv4Enabled { get; init; }
    public required bool IPv6Enabled { get; init; }
    public required ReadOnlySpan<NativeIPAddress> Unicast { get; init; }
    public required ReadOnlySpan<CIDR> Nets { get; init; }
}

delegate void NativeAdapterCallback(ref readonly NativeAdapterInfo info);

static class NativeAdapters {

    public static unsafe void Enumerate(NativeAdapterCallback callback) {
        var family = (uint)ADDRESS_FAMILY.AF_UNSPEC;

        var flags = GET_ADAPTERS_ADDRESSES_FLAGS.GAA_FLAG_SKIP_ANYCAST
            | GET_ADAPTERS_ADDRESSES_FLAGS.GAA_FLAG_SKIP_MULTICAST
            | GET_ADAPTERS_ADDRESSES_FLAGS.GAA_FLAG_SKIP_DNS_SERVER;

        uint bufSize = 0;
        _ = PInvoke.GetAdaptersAddresses(family, flags, default, null, &bufSize);

        var buf = Marshal.AllocHGlobal((int)bufSize);
        try {
            var unicastList = new List<NativeIPAddress>();
            var netsList = new List<CIDR>();
            var itemPtr = (IP_ADAPTER_ADDRESSES_LH*)buf;
            NativeUtils.MustSucceed(PInvoke.GetAdaptersAddresses(family, flags, default, itemPtr, &bufSize));
            while(itemPtr != null) {
                unicastList.Clear();
                var unicastPtr = itemPtr->FirstUnicastAddress;
                while(unicastPtr != null) {
                    var ip = NativeIPAddress.From(unicastPtr->Address.lpSockaddr);
                    unicastList.Add(ip);
                    if(unicastPtr->PrefixOrigin != NL_PREFIX_ORIGIN.IpPrefixOriginWellKnown) {
                        var prefixLen = unicastPtr->OnLinkPrefixLength;
                        if(prefixLen < ip.GetMaxPrefixLen()) {
                            var net = new CIDR(ip.ToPrefix(prefixLen), prefixLen);
                            netsList.Add(net);
                        }
                    }
                    unicastPtr = unicastPtr->Next;
                }
                var info = new NativeAdapterInfo {
                    Guid = NativeUtils.ParseGuid(itemPtr->AdapterName),
                    Luid = itemPtr->Luid,
                    Name = itemPtr->FriendlyName.AsSpan(),
                    Description = itemPtr->Description.AsSpan(),
                    Status = itemPtr->OperStatus,
                    IPv4Enabled = itemPtr->Anonymous2.Anonymous.Ipv4Enabled,
                    IPv6Enabled = itemPtr->Anonymous2.Anonymous.Ipv6Enabled,
                    Unicast = CollectionsMarshal.AsSpan(unicastList),
                    Nets = CollectionsMarshal.AsSpan(netsList),
                };
                callback(in info);
                itemPtr = itemPtr->Next;
            }
        } finally {
            Marshal.FreeHGlobal(buf);
        }
    }
}
