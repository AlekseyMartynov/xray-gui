using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Windows.Win32.NetworkManagement.Ndis;

namespace Project;

static class TunModeRouting {
    static readonly IReadOnlyList<CIDR>
        DefaultOverrideV4,
        DefaultOverrideV6;

    static readonly string
        DefaultOverrideUndoPath,
        TunnelUndoPath;

    static TunModeRouting() {
        // https://github.com/Jigsaw-Code/outline-apps/blob/manager_windows/v1.17.2/client/electron/windows/OutlineService/OutlineService/OutlineService.cs#L608
        // https://github.com/Jigsaw-Code/outline-apps/blob/manager_windows/v1.17.2/client/electron/windows/OutlineService/OutlineService/OutlineService.cs#L673

        DefaultOverrideV4 = [
            (NativeIPAddress.IPv4Zero, 1),
            (new(128), 1),
        ];

        DefaultOverrideV6 = [
            (new([0xfc00]), 7),
            (new([0x2000]), 4),
            (new([0x3000]), 4),
        ];

        var dir = AppContext.BaseDirectory;
        DefaultOverrideUndoPath = Path.Join(dir, "routing_undo_default.bak");
        TunnelUndoPath = Path.Join(dir, "routing_undo_tunnel.bak");
    }

    public static NativeRoute? DefaultV4 { get; private set; }
    public static NativeRoute? DefaultV6 { get; private set; }

    public static void FindDefaults() {
        DefaultV4 = NativeRouting.FindRoute(NativeIPAddress.IPv4Zero, 0);
        DefaultV6 = NativeRouting.FindRoute(NativeIPAddress.IPv6Zero, 0);
    }

    public static void AddDefaultOverride() {
        var undo = LoadUndo(DefaultOverrideUndoPath);

        void TryAdd(NativeRoute route) {
            if(NativeRouting.TryCreateRoute(route)) {
                undo.Add(route);
            }
        }

        try {
            foreach(var dest in DefaultOverrideV4) {
                TryAdd(new() {
                    Dest = dest,
                    Gateway = NativeIPAddress.IPv4Zero,
                    AdapterLuid = TunModeAdapters.TunLuid,
                });
            }
            foreach(var dest in DefaultOverrideV6) {
                TryAdd(new() {
                    Dest = dest,
                    Gateway = NativeIPAddress.IPv6Zero,
                    AdapterLuid = AppConfig.TunModeIPv6 && TunModeAdapters.IPv6TunEnabled
                        ? TunModeAdapters.TunLuid
                        : TunModeAdapters.LoopbackLuid,
                });
            }
        } finally {
            SaveUndo(DefaultOverrideUndoPath, undo);
        }
    }

    public static void AddTunnel() {
        var count = 0;
        var undo = LoadUndo(TunnelUndoPath);
        try {
            foreach(var ip in TunModeServerInfo.IPList) {
                var gatewayRoute = ip.IsIPv4() ? DefaultV4 : DefaultV6;
                if(gatewayRoute == null) {
                    continue;
                }
                count++;
                var route = new NativeRoute {
                    Dest = ip,
                    Gateway = gatewayRoute.Gateway,
                    AdapterLuid = gatewayRoute.AdapterLuid,
                };
                if(NativeRouting.TryCreateRoute(route)) {
                    undo.Add(route);
                }
            }
            if(count < 1) {
                throw new UIException($"No route to address: {TunModeServerInfo.Address}");
            }
        } finally {
            SaveUndo(TunnelUndoPath, undo);
        }
    }

    public static void UndoAll() {
        RunUndo(DefaultOverrideUndoPath);
        RunUndo(TunnelUndoPath);
    }

    static void SaveUndo(string path, List<NativeRoute> routes) {
        File.WriteAllLines(path, routes.ConvertAll(FormatUndoLine));
    }

    static void RunUndo(string path) {
        foreach(var route in LoadUndo(path)) {
            NativeRouting.TryDeleteRoute(route);
        }
        File.Delete(path);
    }

    static List<NativeRoute> LoadUndo(string path) {
        if(!File.Exists(path)) {
            return [];
        }
        var routes = new List<NativeRoute>();
        var fileText = File.ReadAllText(path);
        foreach(var line in fileText.AsSpan().EnumerateLines()) {
            if(line.IsWhiteSpace()) {
                continue;
            }
            if(TryParseUndoLine(line, out var route)) {
                routes.Add(route);
            }
        }
        return routes;
    }

    static string FormatUndoLine(NativeRoute route) {
        return $"{route.Dest} {route.Gateway} {route.AdapterLuid.Value:x}";
    }

    static bool TryParseUndoLine(ReadOnlySpan<char> line, [NotNullWhen(true)] out NativeRoute? route) {
        route = default;

        var dest = default(CIDR);
        var gateway = default(NativeIPAddress);
        var adapterLuidValue = default(ulong);

        var chunkIndex = 0;

        foreach(var r in line.Split(' ')) {
            var chunk = line[r];
            switch(chunkIndex) {
                case 0:
                    if(!CIDR.TryParse(chunk, out dest)) {
                        return false;
                    }
                    break;
                case 1:
                    if(!NativeIPAddress.TryParse(chunk, out gateway)) {
                        return false;
                    }
                    break;
                case 2:
                    if(!ulong.TryParse(chunk, NumberStyles.HexNumber, default, out adapterLuidValue)) {
                        return false;
                    }
                    break;
                default:
                    return false;
            }
            chunkIndex++;
        }

        route = new() {
            Dest = dest,
            Gateway = gateway,
            AdapterLuid = Unsafe.As<ulong, NET_LUID_LH>(ref adapterLuidValue),
        };

        return true;
    }
}
