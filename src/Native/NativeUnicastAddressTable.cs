using Windows.Win32;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.NetworkManagement.Ndis;

namespace Project;

static class NativeUnicastAddressTable {

    public static unsafe void AssignStatic(ADDRESS_FAMILY family, NET_LUID_LH adapterLuid, NativeIPAddress ip, byte prefixLen) {
        var tablePtr = default(MIB_UNICASTIPADDRESS_TABLE*);

        try {
            NativeUtils.MustSucceed(
                PInvoke.GetUnicastIpAddressTable(family, out tablePtr)
            );

            var rowCount = (int)tablePtr->NumEntries;
            var rows = tablePtr->Table.AsSpan(rowCount);

            foreach(var row in rows) {
                if(ShouldDelete(in row)) {
                    NativeUtils.MustSucceed(
                        PInvoke.DeleteUnicastIpAddressEntry(in row)
                    );
                }
            }
        } finally {
            if(tablePtr != null) {
                PInvoke.FreeMibTable(tablePtr);
            }
        }

        bool ShouldDelete(ref readonly MIB_UNICASTIPADDRESS_ROW row) {
            if(row.InterfaceLuid.Value == adapterLuid.Value || NativeIPAddress.From(in row.Address).Equals(ip)) {
                switch(row.PrefixOrigin) {
                    case NL_PREFIX_ORIGIN.IpPrefixOriginManual:
                    case NL_PREFIX_ORIGIN.IpPrefixOriginDhcp:
                        return true;
                }
            }
            return false;
        }

        if(family == ADDRESS_FAMILY.AF_INET6 && ip.Equals(NativeIPAddress.IPv6Zero)) {
            return;
        }

        PInvoke.InitializeUnicastIpAddressEntry(out var newRow);
        newRow.InterfaceLuid = adapterLuid;
        newRow.OnLinkPrefixLength = prefixLen;
        ip.WriteTo(ref newRow.Address);

        NativeUtils.MustSucceed(
            PInvoke.CreateUnicastIpAddressEntry(in newRow)
        );
    }
}
