using Windows.Win32;
using Windows.Win32.NetworkManagement.Ndis;
using Windows.Win32.NetworkManagement.WindowsFilteringPlatform;

namespace Project;

// https://github.com/eycorsican/go-tun2socks/blob/v1.16.11/common/dns/blocker/blocker_windows.go
// https://github.com/Jigsaw-Code/outline-apps/blob/manager_windows/v1.17.2/client/electron/windows/smartdnsblock/smartdnsblock/smartdnsblock.cpp

static class TunModeFilter {
    const string NAME = "xray-gui-filter";

    static readonly Guid SubLayerKey = Guid.NewGuid();

    static FWPM_ENGINE_HANDLE Engine;

    public static void Start() {
        InitEngine();
        InitSubLayer();
        if(AppConfig.TunModeLockdown) {
            Lockdown();
        }
        BlockOutsideDns();
        BlockNonUnicast();
    }

    static unsafe void Lockdown() {
        var blockWeight = 1;
        var permitWeight = 2;
        // Stay within the same layer for proper weight-based rule arbitration
        var layer4 = PInvoke.FWPM_LAYER_OUTBOUND_TRANSPORT_V4;
        var layer6 = PInvoke.FWPM_LAYER_OUTBOUND_TRANSPORT_V6;
        {
            FWPM_FILTER_CONDITION0 ifCond = default;

            var ifLuid = TunModeAdapters.TunLuid;
            InitInterfaceCondition(ref ifCond, &ifLuid, not: true);

            AddBlockFilter(layer4, blockWeight, ifCond);
            AddBlockFilter(layer6, blockWeight, ifCond);
        }
        {
            foreach(var ip in TunModeServerInfo.IPList) {
                PermitCidr(TunModeAdapters.PrimaryLuid, ip, TunModeServerInfo.Port);
            }
        }
        {
            // Critical for OS internal communications:
            // curl: (2) getaddrinfo() thread failed to start
            var loopbackCond = new FWPM_FILTER_CONDITION0 {
                fieldKey = PInvoke.FWPM_CONDITION_FLAGS,
                matchType = FWP_MATCH_TYPE.FWP_MATCH_FLAGS_ALL_SET,
                conditionValue = {
                    type = FWP_DATA_TYPE.FWP_UINT32,
                    Anonymous = {
                        uint32 = PInvoke.FWP_CONDITION_FLAG_IS_LOOPBACK
                    }
                }
            };
            AddPermitFilter(layer4, permitWeight, loopbackCond);
            AddPermitFilter(layer6, permitWeight, loopbackCond);
        }
        {
            var assumeLAN = false;
            foreach(var cidr in TunModeAdapters.PrimaryNets) {
                if(cidr.Prefix.IsRfc1918()) {
                    PermitCidr(TunModeAdapters.PrimaryLuid, cidr);
                    assumeLAN = true;
                }
            }
            if(assumeLAN) {
                ReadOnlySpan<CIDR> allowList = [
                    // Link-local multicast
                    // Protocols: IGMPv3, LLMNR, mDNS
                    (new(224, 0, 0, 0), 24),

                    // Site-local multicast
                    // Protocols: DLNA, SSDP, UPnP, WS-Discovery
                    (new(239, 255, 255, 250), 32),

                    // Link-local broadcast
                    // Protocols: DHCP, NetBIOS legacy mode, custom discovery (Dropbox LAN sync, etc)
                    (new(255, 255, 255, 255), 32),
                ];
                foreach(var cidr in allowList) {
                    PermitCidr(TunModeAdapters.PrimaryLuid, cidr);
                }
            }
        }
        void PermitCidr(NET_LUID_LH ifLuid, CIDR cidr, int port = -1) {
            var anyPort = port < 0;
            var condList = (stackalloc FWPM_FILTER_CONDITION0[anyPort ? 2 : 3]);
            InitInterfaceCondition(ref condList[0], &ifLuid);
            if(!anyPort) {
                InitRemotePortCondition(ref condList[2], (ushort)port);
            }
            if(cidr.IsIPv4()) {
                var addrMask = default(FWP_V4_ADDR_AND_MASK);
                cidr.WriteTo(ref addrMask);
                InitRemoteCidrCondition(ref condList[1], &addrMask);
                AddPermitFilter(layer4, permitWeight, condList);
            } else {
                var addrMask = default(FWP_V6_ADDR_AND_MASK);
                cidr.WriteTo(ref addrMask);
                InitRemoteCidrCondition(ref condList[1], &addrMask);
                AddPermitFilter(layer6, permitWeight, condList);
            }
        }
    }

    static unsafe void BlockOutsideDns() {
        var weight = 3;

        // https://en.wikipedia.org/wiki/List_of_TCP_and_UDP_port_numbers
        ReadOnlySpan<ushort> ports = [53, 853];

        FWPM_FILTER_CONDITION0 portCond = default, ifCond = default;

        var ifLuid = TunModeAdapters.TunLuid;
        InitInterfaceCondition(ref ifCond, &ifLuid, not: true);

        foreach(var port in ports) {
            InitRemotePortCondition(ref portCond, port);
            AddBlockFilter(PInvoke.FWPM_LAYER_OUTBOUND_TRANSPORT_V4, weight, portCond, ifCond);
            AddBlockFilter(PInvoke.FWPM_LAYER_OUTBOUND_TRANSPORT_V6, weight, portCond, ifCond);
        }
    }

    static unsafe void BlockNonUnicast() {
        var weight = 3;

        ReadOnlySpan<CIDR> cidrList = [
            TunModeAdapters.TunBroadcast,
            (new(224), 3), // Class D + Class E
            (new([0xff00]), 8)
        ];

        FWPM_FILTER_CONDITION0 cidrCond = default, ifCond = default;

        var ifLuid = TunModeAdapters.TunLuid;
        InitInterfaceCondition(ref ifCond, &ifLuid);

        foreach(var cidr in cidrList) {
            if(cidr.IsIPv4()) {
                var addrMask = default(FWP_V4_ADDR_AND_MASK);
                cidr.WriteTo(ref addrMask);
                InitRemoteCidrCondition(ref cidrCond, &addrMask);
                AddBlockFilter(PInvoke.FWPM_LAYER_OUTBOUND_IPPACKET_V4, weight, cidrCond, ifCond);
            } else {
                var addrMask = default(FWP_V6_ADDR_AND_MASK);
                cidr.WriteTo(ref addrMask);
                InitRemoteCidrCondition(ref cidrCond, &addrMask);
                AddBlockFilter(PInvoke.FWPM_LAYER_OUTBOUND_IPPACKET_V6, weight, cidrCond, ifCond);
            }
        }
    }

    public static void Stop() {
        if(!Engine.IsNull) {
            NativeUtils.MustSucceed(
                PInvoke.FwpmEngineClose0(Engine)
            );
            Engine = FWPM_ENGINE_HANDLE.Null;
        }
    }

    static unsafe void InitEngine() {
        if(!Engine.IsNull) {
            throw new InvalidOperationException();
        }

        var session = new FWPM_SESSION0 {
            flags = PInvoke.FWPM_SESSION_FLAG_DYNAMIC
        };

        const uint RPC_C_AUTHN_DEFAULT = uint.MaxValue;

        fixed(FWPM_ENGINE_HANDLE* enginePtr = &Engine) {
            NativeUtils.MustSucceed(
                PInvoke.FwpmEngineOpen0(default, RPC_C_AUTHN_DEFAULT, default, &session, enginePtr)
            );
        }
    }

    static unsafe void InitSubLayer() {
        fixed(char* namePtr = NAME) {
            var subLayer = new FWPM_SUBLAYER0 {
                subLayerKey = SubLayerKey,
                weight = ushort.MaxValue,
                displayData = {
                    name = namePtr
                }
            };
            NativeUtils.MustSucceed(
                PInvoke.FwpmSubLayerAdd0(Engine, in subLayer, default)
            );

        }
    }

    static void AddBlockFilter(Guid layerKey, int weight, params ReadOnlySpan<FWPM_FILTER_CONDITION0> conditions) {
        AddFilter(layerKey, (ulong)weight, conditions, FWP_ACTION_TYPE.FWP_ACTION_BLOCK);
    }

    static void AddPermitFilter(Guid layerKey, int weight, params ReadOnlySpan<FWPM_FILTER_CONDITION0> conditions) {
        AddFilter(layerKey, (ulong)weight, conditions, FWP_ACTION_TYPE.FWP_ACTION_PERMIT);
    }

    static unsafe void AddFilter(Guid layerKey, ulong weight, ReadOnlySpan<FWPM_FILTER_CONDITION0> conditions, FWP_ACTION_TYPE actionType) {
        fixed(char* namePtr = NAME)
        fixed(FWPM_FILTER_CONDITION0* conditionPtr = &conditions[0]) {
            var filter = new FWPM_FILTER0 {
                displayData = {
                    name = namePtr
                },

                filterCondition = conditionPtr,
                numFilterConditions = (uint)conditions.Length,

                subLayerKey = SubLayerKey,
                layerKey = layerKey,

                action = {
                    type = actionType
                },

                weight = {
                    type = FWP_DATA_TYPE.FWP_UINT64,
                    Anonymous = {
                        uint64 = &weight
                    }
                }
            };

            NativeUtils.MustSucceed(
                PInvoke.FwpmFilterAdd0(Engine, in filter, default, out _)
            );
        }
    }

    static void InitRemotePortCondition(ref FWPM_FILTER_CONDITION0 cond, ushort port) {
        cond.matchType = FWP_MATCH_TYPE.FWP_MATCH_EQUAL;
        cond.fieldKey = PInvoke.FWPM_CONDITION_IP_REMOTE_PORT;
        cond.conditionValue = new() {
            type = FWP_DATA_TYPE.FWP_UINT16,
            Anonymous = {
                uint16 = port
            }
        };
    }

    static unsafe void InitInterfaceCondition(ref FWPM_FILTER_CONDITION0 cond, NET_LUID_LH* luidPtr, bool not = false) {
        cond.matchType = not ? FWP_MATCH_TYPE.FWP_MATCH_NOT_EQUAL : FWP_MATCH_TYPE.FWP_MATCH_EQUAL;
        cond.fieldKey = PInvoke.FWPM_CONDITION_IP_LOCAL_INTERFACE;
        cond.conditionValue = new() {
            type = FWP_DATA_TYPE.FWP_UINT64,
            Anonymous = {
                uint64 = (ulong*)luidPtr
            }
        };
    }

    static unsafe void InitRemoteCidrCondition(ref FWPM_FILTER_CONDITION0 cond, FWP_V4_ADDR_AND_MASK* addrMask) {
        cond.matchType = FWP_MATCH_TYPE.FWP_MATCH_EQUAL;
        cond.fieldKey = PInvoke.FWPM_CONDITION_IP_REMOTE_ADDRESS;
        cond.conditionValue = new() {
            type = FWP_DATA_TYPE.FWP_V4_ADDR_MASK,
            Anonymous = {
                v4AddrMask = addrMask
            }
        };
    }

    static unsafe void InitRemoteCidrCondition(ref FWPM_FILTER_CONDITION0 cond, FWP_V6_ADDR_AND_MASK* addrMask) {
        cond.matchType = FWP_MATCH_TYPE.FWP_MATCH_EQUAL;
        cond.fieldKey = PInvoke.FWPM_CONDITION_IP_REMOTE_ADDRESS;
        cond.conditionValue = new() {
            type = FWP_DATA_TYPE.FWP_V6_ADDR_MASK,
            Anonymous = {
                v6AddrMask = addrMask
            }
        };
    }
}

