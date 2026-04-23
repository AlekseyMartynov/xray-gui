using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.NetworkManagement.Ndis;

namespace Project;

class NativeRoute {
    public required CIDR Dest { get; init; }
    public required NativeIPAddress Gateway { get; init; }
    public required NET_LUID_LH AdapterLuid { get; init; }
}

static class NativeRouting {

    public static NativeRoute? FindRoute(NativeIPAddress destPrefix, byte destPrefixLen) {
        return FindRoute((destPrefix, destPrefixLen));
    }

    public static unsafe NativeRoute? FindRoute(CIDR dest) {
        var tablePtr = default(MIB_IPFORWARD_TABLE2*);

        try {
            NativeUtils.MustSucceed(PInvoke.GetIpForwardTable2(dest.Prefix.GetFamily(), out tablePtr));

            var rowCount = (int)tablePtr->NumEntries;
            var rows = tablePtr->Table.AsSpan(rowCount);

            var bestRowIndex = -1;
            var bestTotalMetric = default(uint);

            for(var i = 0; i < rowCount; i++) {
                var candidate = rows[i];
                var candidateDest = candidate.DestinationPrefix;
                if(candidateDest.PrefixLength != dest.PrefixLen) {
                    continue;
                }
                if(!dest.Prefix.Equals(NativeIPAddress.From(in candidateDest.Prefix))) {
                    continue;
                }
                var candidateInterface = GetInterface(in candidate);
                if(!candidateInterface.Connected) {
                    continue;
                }
                var candidateTotalMetric = candidate.Metric + candidateInterface.Metric;
                if(bestRowIndex < 0 || candidateTotalMetric < bestTotalMetric) {
                    bestRowIndex = i;
                    bestTotalMetric = candidateTotalMetric;
                }
            }

            if(bestRowIndex < 0) {
                return null;
            }

            var bestRow = rows[bestRowIndex];

            return new() {
                Dest = dest,
                Gateway = NativeIPAddress.From(in bestRow.NextHop),
                AdapterLuid = bestRow.InterfaceLuid,
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

        if(result == WIN32_ERROR.ERROR_FILE_NOT_FOUND) {
            // Can happen after reboot
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
            InterfaceLuid = route.AdapterLuid,
        };

        route.Dest.WriteTo(ref row.DestinationPrefix);
        route.Gateway.WriteTo(ref row.NextHop);

        return row;
    }

    static MIB_IPINTERFACE_ROW GetInterface(ref readonly MIB_IPFORWARD_ROW2 row) {
        var result = default(MIB_IPINTERFACE_ROW);
        PInvoke.InitializeIpInterfaceEntry(ref result);
        result.Family = row.DestinationPrefix.Prefix.si_family;
        result.InterfaceLuid = row.InterfaceLuid;
        NativeUtils.MustSucceed(PInvoke.GetIpInterfaceEntry(ref result));
        return result;
    }
}
