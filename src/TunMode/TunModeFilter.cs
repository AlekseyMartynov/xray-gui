using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.WinSock;
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
        BlockOutsideDns();
    }

    static void BlockOutsideDns() {
        // https://en.wikipedia.org/wiki/List_of_TCP_and_UDP_port_numbers
        ReadOnlySpan<ushort> ports = [53, 853];

        FWPM_FILTER_CONDITION0 portCond = default, ifCond = default;

        foreach(var port in ports) {
            InitRemotePortCondition(ref portCond, port);
            {
                InitInterfaceCondition(ref ifCond, TunModeAdapters.IPv4TunIndex, not: true);
                AddBlockFilter(PInvoke.FWPM_LAYER_OUTBOUND_TRANSPORT_V4, portCond, ifCond);
            }
            {
                InitInterfaceCondition(ref ifCond, TunModeAdapters.IPv6TunIndex, not: true);
                AddBlockFilter(PInvoke.FWPM_LAYER_OUTBOUND_TRANSPORT_V6, portCond, ifCond);
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

    static void AddBlockFilter(Guid layerKey, params ReadOnlySpan<FWPM_FILTER_CONDITION0> conditions) {
        AddFilter(layerKey, 1, conditions, FWP_ACTION_TYPE.FWP_ACTION_BLOCK);
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

    static void InitInterfaceCondition(ref FWPM_FILTER_CONDITION0 cond, uint index, bool not = false) {
        cond.matchType = not ? FWP_MATCH_TYPE.FWP_MATCH_NOT_EQUAL : FWP_MATCH_TYPE.FWP_MATCH_EQUAL;
        cond.fieldKey = PInvoke.FWPM_CONDITION_INTERFACE_INDEX;
        cond.conditionValue = new() {
            type = FWP_DATA_TYPE.FWP_UINT32,
            Anonymous = {
                uint32 = index
            }
        };
    }
}

