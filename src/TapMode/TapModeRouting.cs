using System.Diagnostics.CodeAnalysis;

namespace Project;

static class TapModeRouting {
    static readonly IReadOnlyList<(NativeIPAddress, byte)>
        DefaultOverrideV4,
        DefaultOverrideV6;

    static readonly string
        DefaultOverrideUndoPath,
        TunnelUndoPath;

    static TapModeRouting() {
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
        DefaultOverrideUndoPath = Path.Join(dir, "routing_undo_default.cmd");
        TunnelUndoPath = Path.Join(dir, "routing_undo_tunnel.cmd");
    }

    public static void AddDefaultOverride() {
        // Outline also adds bypass routes for Special-Purpose IPv4 ranges (RFC6890)
        // https://github.com/Jigsaw-Code/outline-apps/blob/manager_windows/v1.17.2/client/electron/windows/OutlineService/OutlineService/OutlineService.cs#L693
        // However, this might be redundant:
        // - Windows typically adds On-link LAN routes via DHCP
        // - https://github.com/Jigsaw-Code/outline-server/issues/545

        var undo = LoadUndo(DefaultOverrideUndoPath);

        void TryAdd(NativeRoute route) {
            if(NativeRouting.TryCreateRoute(route)) {
                undo.Add(route);
            }
        }

        try {
            foreach(var (prefix, prefixLen) in DefaultOverrideV4) {
                TryAdd(new() {
                    DestPrefix = prefix,
                    DestPrefixLen = prefixLen,
                    Gateway = TapModeAdapters.TapGateway,
                    AdapterIndex = TapModeAdapters.TapIndex,
                });
            }
            foreach(var (prefix, prefixLen) in DefaultOverrideV6) {
                TryAdd(new() {
                    DestPrefix = prefix,
                    DestPrefixLen = prefixLen,
                    Gateway = NativeIPAddress.IPv6Zero,
                    AdapterIndex = TapModeAdapters.IPv6LoopbackIndex,
                });
            }
        } finally {
            SaveUndo(DefaultOverrideUndoPath, undo);
        }
    }

    public static void AddTunnel() {
        var defaultV4 = NativeRouting.FindRoute(NativeIPAddress.IPv4Zero, 0) ?? throw new Exception("No default IPv4 route found");
        var undo = LoadUndo(TunnelUndoPath);
        try {
            foreach(var ip in TapModeServerInfo.IPv4List) {
                var route = new NativeRoute {
                    DestPrefix = ip,
                    DestPrefixLen = 32,
                    Gateway = defaultV4.Gateway,
                    AdapterIndex = defaultV4.AdapterIndex,
                };
                if(NativeRouting.TryCreateRoute(route)) {
                    undo.Add(route);
                }
            }
        } finally {
            SaveUndo(TunnelUndoPath, undo);
        }
    }

    public static void UndoAll() {
        UndoDefaultOverride();
        UndoTunnel();
    }

    public static void UndoDefaultOverride() {
        RunUndo(DefaultOverrideUndoPath);
    }

    public static void UndoTunnel() {
        RunUndo(TunnelUndoPath);
    }

    static void SaveUndo(string path, List<NativeRoute> routes) {
        File.WriteAllLines(path, routes.Select(FormatUndoLine));
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
        return $"route delete {route.DestPrefix}/{route.DestPrefixLen} {route.Gateway} if {route.AdapterIndex}";
    }

    static bool TryParseUndoLine(ReadOnlySpan<char> line, [NotNullWhen(true)] out NativeRoute? route) {
        route = default;

        var destPrefix = default(NativeIPAddress);
        var destPrefixLen = default(byte);
        var gateway = default(NativeIPAddress);
        var adapterIndex = default(uint);

        var chunkIndex = 0;

        foreach(var r in line.Split(' ')) {
            var chunk = line[r];
            switch(chunkIndex) {
                case 0:
                case 1:
                case 4:
                    break;
                case 2:
                    if(!chunk.TrySplit('/', out var destPrefixText, out var destPrefixLenText)) {
                        return false;
                    }
                    if(!NativeIPAddress.TryParse(destPrefixText, out destPrefix)) {
                        return false;
                    }
                    if(!byte.TryParse(destPrefixLenText, out destPrefixLen)) {
                        return false;
                    }
                    break;
                case 3:
                    if(!NativeIPAddress.TryParse(chunk, out gateway)) {
                        return false;
                    }
                    break;
                case 5:
                    if(!uint.TryParse(chunk, out adapterIndex)) {
                        return false;
                    }
                    break;
                default:
                    return false;
            }
            chunkIndex++;
        }

        route = new() {
            DestPrefix = destPrefix,
            DestPrefixLen = destPrefixLen,
            Gateway = gateway,
            AdapterIndex = adapterIndex,
        };

        return true;
    }
}
