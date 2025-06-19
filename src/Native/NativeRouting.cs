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
        var tablePtr = default(MIB_IPFORWARD_TABLE2*);

        try {
            NativeUtils.MustSucceed(PInvoke.GetIpForwardTable2(destPrefix.GetFamily(), out tablePtr));

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

        route.DestPrefix.WriteTo(ref row.DestinationPrefix.Prefix);
        route.Gateway.WriteTo(ref row.NextHop);

        return row;
    }
}
