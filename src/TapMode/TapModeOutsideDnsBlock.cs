using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.WindowsFilteringPlatform;

namespace Project;

// https://github.com/eycorsican/go-tun2socks/blob/v1.16.11/common/dns/blocker/blocker_windows.go
// https://github.com/Jigsaw-Code/outline-apps/blob/manager_windows/v1.17.2/client/electron/windows/smartdnsblock/smartdnsblock/smartdnsblock.cpp

static class TapModeOutsideDnsBlock {
    const string NAME = "xray-gui-outside-dns-block";

    static readonly Guid SubLayerKey = Guid.NewGuid();

    static HANDLE Engine;

    public static void Start() {
        InitEngine();
        InitSubLayer();

        ReadOnlySpan<FWPM_FILTER_CONDITION0> blockConditions = [
            new() {
                fieldKey = PInvoke.FWPM_CONDITION_IP_PROTOCOL,
                matchType = FWP_MATCH_TYPE.FWP_MATCH_EQUAL,
                conditionValue = {
                    type = FWP_DATA_TYPE.FWP_UINT8,
                    Anonymous = {
                        uint8 = (byte)IPPROTO.IPPROTO_UDP
                    }
                }
            },
            new() {
                fieldKey = PInvoke.FWPM_CONDITION_IP_REMOTE_PORT,
                matchType = FWP_MATCH_TYPE.FWP_MATCH_EQUAL,
                conditionValue = {
                    type = FWP_DATA_TYPE.FWP_UINT16,
                    Anonymous = {
                        uint16 = 53
                    }
                }
            }
        ];

        AddFilter(PInvoke.FWPM_LAYER_ALE_AUTH_CONNECT_V4, 1, blockConditions, FWP_ACTION_TYPE.FWP_ACTION_BLOCK);
        AddFilter(PInvoke.FWPM_LAYER_ALE_AUTH_CONNECT_V6, 1, blockConditions, FWP_ACTION_TYPE.FWP_ACTION_BLOCK);

        var permitCondition = new FWPM_FILTER_CONDITION0() {
            fieldKey = PInvoke.FWPM_CONDITION_INTERFACE_INDEX,
            matchType = FWP_MATCH_TYPE.FWP_MATCH_EQUAL,
            conditionValue = {
                type = FWP_DATA_TYPE.FWP_UINT32,
                Anonymous = {
                    uint32 = TapModeAdapters.TapIndex
                }
            }
        };

        AddFilter(PInvoke.FWPM_LAYER_ALE_AUTH_CONNECT_V4, 2, [permitCondition], FWP_ACTION_TYPE.FWP_ACTION_PERMIT);
    }

    public static void Stop() {
        if(!Engine.IsNull) {
            NativeUtils.MustSucceed(
                PInvoke.FwpmEngineClose0(Engine)
            );
            Engine = HANDLE.Null;
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

        fixed(HANDLE* enginePtr = &Engine) {
            NativeUtils.MustSucceed(
                PInvoke.FwpmEngineOpen0(default, RPC_C_AUTHN_DEFAULT, default, session, enginePtr)
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

            ulong id = default;

            NativeUtils.MustSucceed(
                PInvoke.FwpmFilterAdd0(Engine, in filter, default, &id)
            );
        }
    }
}

