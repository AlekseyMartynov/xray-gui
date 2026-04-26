using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.Ndis;

namespace Project;

using MeasuredGateway = (NativeIPAddress Gateway, uint Metric);

class BestDefaultRoute {
    public static readonly BestDefaultRoute Empty = new();

    public readonly NET_LUID_LH Luid;
    public readonly bool HasIPv4, HasIPv6;

    readonly NativeIPAddress IPv4Gateway, IPv6Gateway;

    private BestDefaultRoute() {
        IPv4Gateway = NativeIPAddress.IPv4Zero;
        IPv6Gateway = NativeIPAddress.IPv6Zero;
    }

    public BestDefaultRoute(IReadOnlyList<MeasuredNativeRoute> defaultRoutes) : this() {
        var adapterProfiles = (stackalloc (NET_LUID_LH Luid, MeasuredGateway V4, MeasuredGateway V6)[defaultRoutes.Count]);
        adapterProfiles.Clear();

        foreach(var r in defaultRoutes) {
            foreach(ref var p in adapterProfiles) {
                if(p.Luid.IsEmpty) {
                    p.Luid = r.AdapterLuid;
                    p.V4 = (NativeIPAddress.IPv4Zero, uint.MaxValue);
                    p.V6 = (NativeIPAddress.IPv6Zero, uint.MaxValue);
                }
                if(p.Luid == r.AdapterLuid) {
                    if(r.Dest.IsIPv4()) {
                        if(r.TotalMetric < p.V4.Metric) {
                            p.V4 = (r.Gateway, r.TotalMetric);
                        }
                    } else {
                        if(r.TotalMetric < p.V6.Metric) {
                            p.V6 = (r.Gateway, r.TotalMetric);
                        }
                    }
                    break;
                }
            }
        }

        var (bestProtoRank, bestMetricRank) = (int.MaxValue, uint.MaxValue);
        foreach(var (luid, v4, v6) in adapterProfiles) {
            if(luid.IsEmpty) {
                break;
            }
            var has4 = v4.Metric < uint.MaxValue;
            var has6 = v6.Metric < uint.MaxValue;
            var protoRank = (has4 && has6) ? 0 : has4 ? 1 : 2;
            var metricRank = uint.Min(v4.Metric, v6.Metric);
            if((protoRank, metricRank).CompareTo((bestProtoRank, bestMetricRank)) >= 0) {
                continue;
            }
            (bestProtoRank, bestMetricRank) = (protoRank, metricRank);
            Luid = luid;
            HasIPv4 = has4;
            HasIPv6 = has6;
            IPv4Gateway = v4.Gateway;
            IPv6Gateway = v6.Gateway;
        }
    }

    public bool TryGetGateway(ADDRESS_FAMILY family, out NativeIPAddress result) {
        if(family == ADDRESS_FAMILY.AF_INET) {
            result = IPv4Gateway;
            return HasIPv4;
        }
        if(family == ADDRESS_FAMILY.AF_INET6) {
            result = IPv6Gateway;
            return HasIPv6;
        }
        throw new NotSupportedException();
    }
}
