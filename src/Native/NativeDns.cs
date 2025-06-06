using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.Dns;

namespace Project;

static class NativeDns {

    public static unsafe IReadOnlyList<NativeIPAddress> QueryIPv4(string domain) {
        var resultsPtr = default(DNS_RECORDA*);

        try {
            var queryResult = Query(domain, DNS_TYPE.DNS_TYPE_A, out resultsPtr);

            if(queryResult == WIN32_ERROR.DNS_ERROR_RCODE_NAME_ERROR) {
                return [];
            }

            if((int)queryResult == PInvoke.DNS_INFO_NO_RECORDS) {
                return [];
            }

            NativeUtils.MustSucceed(queryResult);

            var list = new List<NativeIPAddress>();
            var recordPtr = resultsPtr;

            while(recordPtr != null) {
                var type = (DNS_TYPE)recordPtr->wType;
                if(type == DNS_TYPE.DNS_TYPE_A) {
                    list.Add(new(recordPtr->Data.A.IpAddress));
                }
                recordPtr = recordPtr->pNext;
            }

            return list;
        } finally {
            if(resultsPtr != null) {
                DnsRecordListFree(resultsPtr, DNS_FREE_TYPE.DnsFreeRecordList);
            }
        }
    }

    static unsafe WIN32_ERROR Query(string domain, DNS_TYPE type, out DNS_RECORDA* results) {
        return PInvoke.DnsQuery_W(
            domain,
            type,
            DNS_QUERY_OPTIONS.DNS_QUERY_STANDARD,
            default,
            out results,
            default
        );
    }

    // https://github.com/microsoft/CsWin32/issues/1425
    [DllImport("dnsapi", CallingConvention = CallingConvention.Winapi)]
    [SuppressMessage("", "SYSLIB1054")]
    [SuppressMessage("", "IDE0079")]
    static extern unsafe void DnsRecordListFree(DNS_RECORDA* p, in DNS_FREE_TYPE t);
}
