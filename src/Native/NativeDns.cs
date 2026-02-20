using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.Dns;

namespace Project;

static class NativeDns {

    public static IReadOnlyList<NativeIPAddress> QueryIP(string domain, bool v4 = true, bool v6 = true, ReadOnlySpan<NativeIPAddress> servers = default) {
        // https://stackoverflow.com/a/9628315
        var extra = (stackalloc uint[1 + servers.Length]);
        extra[0] = (uint)servers.Length;
        for(var i = 0; i < servers.Length; i++) {
            extra[1 + i] = servers[i].ToUInt32();
        }

        var results = new List<NativeIPAddress>();
        if(v4) {
            QueryIP(domain, (ushort)PInvoke.DNS_TYPE_A, extra, results);
        }
        if(v6) {
            QueryIP(domain, (ushort)PInvoke.DNS_TYPE_AAAA, extra, results);
        }
        return results;
    }

    static unsafe void QueryIP(string domain, ushort type, ReadOnlySpan<uint> extra, List<NativeIPAddress> results) {
        var resultsPtr = default(DNS_RECORDA*);

        try {
            var queryResult = Query(domain, type, extra, out resultsPtr);

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
                if(recordPtr->wType == type) {
                    results.Add(NativeIPAddress.From(recordPtr));
                } else {
                    // DNS_TYPE_CNAME
                    // https://stackoverflow.com/a/52714251
                }
                recordPtr = recordPtr->pNext;
            }
        } finally {
            if(resultsPtr != null) {
                PInvoke.DnsFree(resultsPtr, DNS_FREE_TYPE.DnsFreeRecordList);
            }
        }
    }

    static unsafe WIN32_ERROR Query(string domain, ushort type, ReadOnlySpan<uint> extra, out DNS_RECORDA* results) {
        fixed(void* extraPtr = extra) {
            return PInvoke.DnsQuery_W(
                domain,
                type,
                DNS_QUERY_OPTIONS.DNS_QUERY_STANDARD,
                extraPtr,
                out results,
                out _
            );
        }
    }
}
