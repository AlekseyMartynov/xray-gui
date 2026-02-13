using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;

namespace Project;

static class NativeRestrictedTokens {
    public static readonly HANDLE Constrained, NormalUser, FullyTrusted;

    static unsafe NativeRestrictedTokens() {
        HANDLE currentProcToken;

        NativeUtils.MustSucceed(
            PInvoke.OpenProcessToken(
                PInvoke.GetCurrentProcess(),
                TOKEN_ACCESS_MASK.TOKEN_QUERY | TOKEN_ACCESS_MASK.TOKEN_DUPLICATE | TOKEN_ACCESS_MASK.TOKEN_ASSIGN_PRIMARY,
                &currentProcToken
            )
        );

        Constrained = CreateRestrictedToken(currentProcToken, PInvoke.SAFER_LEVELID_CONSTRAINED);
        NormalUser = CreateRestrictedToken(currentProcToken, PInvoke.SAFER_LEVELID_NORMALUSER);
        FullyTrusted = CreateRestrictedToken(currentProcToken, PInvoke.SAFER_LEVELID_FULLYTRUSTED);

        SetMediumIntegrity([
            Constrained,
            NormalUser,
        ]);
    }

    static unsafe HANDLE CreateRestrictedToken(HANDLE baseToken, uint levelId) {
        // Using deprecated SRP APIs
        // https://learn.microsoft.com/windows-server/identity/software-restriction-policies/software-restriction-policies
        // They need very little code and MS "deprecated" typically means
        // "won't get new features" rather than "will be removed"

        // Alternative (verbose, ask AI for code samples):
        // https://learn.microsoft.com/windows/win32/api/securitybaseapi/nf-securitybaseapi-createrestrictedtoken

        // Explorer token clone trick (https://stackoverflow.com/a/40687129)
        // does not work with AssignProcessToJobObject because process launched with such token
        // is implicitly assigned to Explorer's job

        SAFER_LEVEL_HANDLE levelHandle = default;
        try {
            NativeUtils.MustSucceed(
                PInvoke.SaferCreateLevel(PInvoke.SAFER_SCOPEID_MACHINE, levelId, default, out levelHandle)
            );

            HANDLE result = default;

            NativeUtils.MustSucceed(
                PInvoke.SaferComputeTokenFromLevel(levelHandle, baseToken, &result, default)
            );

            return result;
        } finally {
            if(!levelHandle.IsNull) {
                PInvoke.SaferCloseLevel(levelHandle);
            }
        }
    }

    static unsafe void SetMediumIntegrity(ReadOnlySpan<HANDLE> tokens) {
        // Suggested in
        // - https://www.meziantou.net/starting-a-process-as-normal-user-from-a-process-running-as-administrator.htm
        // - https://stackoverflow.com/a/40687129

        var sidSize = PInvoke.SECURITY_MAX_SID_SIZE;
        var sidPtr = (PSID)Marshal.AllocHGlobal((int)sidSize);

        try {
            NativeUtils.MustSucceed(
                PInvoke.CreateWellKnownSid(WELL_KNOWN_SID_TYPE.WinMediumLabelSid, default, sidPtr, ref sidSize)
            );

            var tokenInfoSize = (uint)Marshal.SizeOf<TOKEN_MANDATORY_LABEL>();

            var tokenInfo = new TOKEN_MANDATORY_LABEL {
                Label = {
                    Attributes = PInvoke.SE_GROUP_INTEGRITY,
                    Sid = sidPtr
                }
            };

            foreach(var token in tokens) {
                NativeUtils.MustSucceed(
                    PInvoke.SetTokenInformation(
                        token,
                        TOKEN_INFORMATION_CLASS.TokenIntegrityLevel,
                        &tokenInfo,
                        tokenInfoSize
                    )
                );
            }
        } finally {
            Marshal.FreeHGlobal(sidPtr);
        }
    }
}
