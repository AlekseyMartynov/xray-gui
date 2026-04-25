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

class MeasuredNativeRoute : NativeRoute {
    public required uint TotalMetric { get; init; }
}

static class NativeRouting {

    public static IReadOnlyList<MeasuredNativeRoute> FindRoutes(NativeIPAddress destPrefix, byte destPrefixLen) {
        return FindRoutes((destPrefix, destPrefixLen));
    }

    public static unsafe IReadOnlyList<MeasuredNativeRoute> FindRoutes(CIDR dest) {
        var tablePtr = default(MIB_IPFORWARD_TABLE2*);

        try {
            var result = new List<MeasuredNativeRoute>();

            NativeUtils.MustSucceed(PInvoke.GetIpForwardTable2(dest.Prefix.GetFamily(), out tablePtr));

            var rowCount = (int)tablePtr->NumEntries;
            var rows = tablePtr->Table.AsSpan(rowCount);

            for(var i = 0; i < rowCount; i++) {
                var row = rows[i];
                var rowDest = row.DestinationPrefix;
                if(rowDest.PrefixLength != dest.PrefixLen) {
                    continue;
                }
                if(!dest.Prefix.Equals(NativeIPAddress.From(in rowDest.Prefix))) {
                    continue;
                }
                var rowInterface = GetInterface(in row);
                if(!rowInterface.Connected) {
                    continue;
                }
                result.Add(new() {
                    Dest = dest,
                    Gateway = NativeIPAddress.From(in row.NextHop),
                    AdapterLuid = row.InterfaceLuid,
                    TotalMetric = row.Metric + rowInterface.Metric
                });
            }

            return result;

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
