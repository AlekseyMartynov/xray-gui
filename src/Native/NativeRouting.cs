using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;

namespace Project;

class NativeRoute {
    public required NativeIPAddress DestPrefix { get; init; }
    public required byte DestPrefixLen { get; init; }
    public required NativeIPAddress Gateway { get; init; }
    public required uint AdapterIndex { get; init; }
}

static class NativeRouting {

    public static unsafe NativeRoute? FindRoute(NativeIPAddress destPrefix, byte destPrefixLen) {
        var family = destPrefix.IsIPv4()
            ? ADDRESS_FAMILY.AF_INET
            : ADDRESS_FAMILY.AF_INET6;

        var tablePtr = default(MIB_IPFORWARD_TABLE2*);

        try {
            NativeUtils.MustSucceed(PInvoke.GetIpForwardTable2(family, out tablePtr));

            var rowCount = (int)tablePtr->NumEntries;
            var rows = tablePtr->Table.AsSpan(rowCount);

            var bestRowIndex = -1;

            for(var i = 0; i < rowCount; i++) {
                var candidate = rows[i];
                var candidateDest = candidate.DestinationPrefix;
                if(candidateDest.PrefixLength != destPrefixLen) {
                    continue;
                }
                if(!destPrefix.Equals(NativeIPAddress.From(in candidateDest.Prefix))) {
                    continue;
                }
                if(bestRowIndex < 0 || candidate.Metric < rows[bestRowIndex].Metric) {
                    bestRowIndex = i;
                }
            }

            if(bestRowIndex < 0) {
                return null;
            }

            var bestRow = rows[bestRowIndex];

            return new() {
                DestPrefix = destPrefix,
                DestPrefixLen = destPrefixLen,
                Gateway = NativeIPAddress.From(in bestRow.NextHop),
                AdapterIndex = bestRow.InterfaceIndex,
            };

        } finally {
            if(tablePtr != null) {
                PInvoke.FreeMibTable(tablePtr);
            }
        }
    }

    public static bool TryCreateRoute(NativeRoute route) {
        var row = ToNativeRow(route);
        var result = PInvoke.CreateIpForwardEntry2(in row);

        if(result == WIN32_ERROR.ERROR_OBJECT_ALREADY_EXISTS) {
            return false;
        }

        NativeUtils.MustSucceed(result);

        return true;
    }

    public static bool TryDeleteRoute(NativeRoute route) {
        var row = ToNativeRow(route);
        var result = PInvoke.DeleteIpForwardEntry2(in row);

        if(result == WIN32_ERROR.ERROR_NOT_FOUND) {
            return false;
        }

        NativeUtils.MustSucceed(result);

        return true;
    }

    static MIB_IPFORWARD_ROW2 ToNativeRow(NativeRoute route) {
        var row = new MIB_IPFORWARD_ROW2 {
            ValidLifetime = PInvoke.INFINITE,
            PreferredLifetime = PInvoke.INFINITE,
            Protocol = NL_ROUTE_PROTOCOL.MIB_IPPROTO_NETMGMT,
            InterfaceIndex = route.AdapterIndex,
            DestinationPrefix = {
                PrefixLength = route.DestPrefixLen
            }
        };

        SetIPAddr(ref row.DestinationPrefix.Prefix, route.DestPrefix);
        SetIPAddr(ref row.NextHop, route.Gateway);

        return row;
    }

    static Span<byte> AsBytes(ref SOCKADDR_INET addr) {
        if(addr.si_family == ADDRESS_FAMILY.AF_INET) {
            return NativeUtils.Cast<uint, byte>(ref addr.Ipv4.sin_addr.S_un.S_addr);
        } else {
            return addr.Ipv6.sin6_addr.u.Byte.AsSpan();
        }
    }

    static void SetIPAddr(ref SOCKADDR_INET addr, in NativeIPAddress value) {
        addr.si_family = value.IsIPv4()
            ? ADDRESS_FAMILY.AF_INET
            : ADDRESS_FAMILY.AF_INET6;
        value.TryWriteBytes(AsBytes(ref addr));
    }
}
