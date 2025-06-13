using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.Dns;

namespace Project;

static class NativeDns {

    public static IReadOnlyList<NativeIPAddress> QueryIP(string domain, bool v4 = true, bool v6 = true) {
        var results = new List<NativeIPAddress>();
        if(v4) {
            QueryIP(domain, DNS_TYPE.DNS_TYPE_A, results);
        }
        if(v6) {
            QueryIP(domain, DNS_TYPE.DNS_TYPE_AAAA, results);
        }
        return results;
    }

    static unsafe void QueryIP(string domain, DNS_TYPE type, List<NativeIPAddress> results) {
        var resultsPtr = default(DNS_RECORDA*);

        try {
            var queryResult = Query(domain, type, out resultsPtr);

            if(queryResult == WIN32_ERROR.DNS_ERROR_RCODE_NAME_ERROR) {
                return;
            }

            if((int)queryResult == PInvoke.DNS_INFO_NO_RECORDS) {
                return;
            }

            if(queryResult == WIN32_ERROR.ERROR_TIMEOUT) {
                throw new UIException("DNS query timeout for " + domain);
            }

            NativeUtils.MustSucceed(queryResult);

            var recordPtr = resultsPtr;

            while(recordPtr != null) {
                results.Add(NativeIPAddress.From(recordPtr));
                recordPtr = recordPtr->pNext;
            }
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
