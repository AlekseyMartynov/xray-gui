using Windows.Win32;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;

namespace Project;

static class NativeUnicastAddressTable {

    public static unsafe void AssignStatic(uint adapterIndex, NativeIPAddress ip, byte prefixLen) {
        var tablePtr = default(MIB_UNICASTIPADDRESS_TABLE*);

        try {
            NativeUtils.MustSucceed(
                PInvoke.GetUnicastIpAddressTable(ADDRESS_FAMILY.AF_UNSPEC, out tablePtr)
            );

            var rowCount = (int)tablePtr->NumEntries;
            var rows = tablePtr->Table.AsSpan(rowCount);

            foreach(var row in rows) {
                if(row.InterfaceIndex != adapterIndex) {
                    continue;
                }
                switch(row.PrefixOrigin) {
                    case NL_PREFIX_ORIGIN.IpPrefixOriginManual:
                    case NL_PREFIX_ORIGIN.IpPrefixOriginDhcp:
                        NativeUtils.MustSucceed(
                            PInvoke.DeleteUnicastIpAddressEntry(in row)
                        );
                        break;
                }
            }
        } finally {
            if(tablePtr != null) {
                PInvoke.FreeMibTable(tablePtr);
            }
        }

        PInvoke.InitializeUnicastIpAddressEntry(out var newRow);
        newRow.InterfaceIndex = adapterIndex;
        newRow.OnLinkPrefixLength = prefixLen;
        ip.WriteTo(ref newRow.Address);

        NativeUtils.MustSucceed(
            PInvoke.CreateUnicastIpAddressEntry(in newRow)
        );
    }
}
