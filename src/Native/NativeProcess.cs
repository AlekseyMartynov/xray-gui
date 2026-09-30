using Microsoft.Win32.SafeHandles;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.JobObjects;
using Windows.Win32.System.Threading;

namespace Project;

class NativeProcess : IDisposable {
    static readonly HANDLE AntiOrphanJobObject = CreateAntiOrphanJobObject();

    readonly HANDLE OutputReadPipe;
    readonly PROCESS_INFORMATION ProcInfo;

    readonly NativeWaitHandle ProcWaitHandle;
    readonly RegisteredWaitHandle ProcWaitRegistration;

    bool Exited;

    public unsafe NativeProcess(string commandLine, string? workDir = null, string[]? env = null, Action? exitHandler = null, HANDLE accessToken = default, bool redirectOutput = false) {
        var si = new STARTUPINFOW {
            cb = (uint)Unsafe.SizeOf<STARTUPINFOW>(),
        };

        var flags = PROCESS_CREATION_FLAGS.CREATE_SUSPENDED | PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;

        if(!redirectOutput && AppConfig.ProcConsole) {
            flags |= PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE;
        } else {
            flags |= PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW;
        }

        var commandLineSpan = (stackalloc char[1 + commandLine.Length]);
        commandLine.CopyTo(commandLineSpan);
        commandLineSpan[commandLine.Length] = '\0';

        var envBuf = default(string);
        if(env != null && env.Length > 0) {
            envBuf = String.Join('\0', env) + "\0\0";
        }

        if(accessToken.IsNull) {
            accessToken = NativeRestrictedTokens.NormalUser;
        }

        var outputWritePipe = HANDLE.Null;

        try {
            if(redirectOutput) {
                var pipeAttrs = new SECURITY_ATTRIBUTES {
                    nLength = (uint)Unsafe.SizeOf<SECURITY_ATTRIBUTES>(),
                    bInheritHandle = true
                };
                NativeUtils.MustSucceed(
                    PInvoke.CreatePipe(out OutputReadPipe, out outputWritePipe, pipeAttrs, default)
                );
                NativeUtils.MustSucceed(
                    PInvoke.SetHandleInformation(OutputReadPipe, (uint)HANDLE_FLAGS.HANDLE_FLAG_INHERIT, 0)
                );
                si.dwFlags |= STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES;
                si.hStdOutput = outputWritePipe;
                si.hStdError = outputWritePipe;
            }

            fixed(void* envBufPtr = envBuf) {
                NativeUtils.MustSucceed(
                    PInvoke.CreateProcessAsUser(
                        accessToken,
                        default,
                        ref commandLineSpan,
                        default, default, redirectOutput,
                        flags,
                        envBufPtr,
                        workDir,
                        in si,
                        out ProcInfo
                    )
                );
            }

            NativeUtils.MustSucceed(PInvoke.AssignProcessToJobObject(AntiOrphanJobObject, ProcInfo.hProcess));

            if(PInvoke.ResumeThread(ProcInfo.hThread) != 1) {
                throw new InvalidOperationException();
            }
        } catch {
            CloseProcHandles();
            throw;
        } finally {
            NativeUtils.TryCloseHandle(outputWritePipe);
        }

        ProcWaitHandle = new NativeWaitHandle(ProcInfo.hProcess);

        ProcWaitRegistration = ThreadPool.RegisterWaitForSingleObject(
            ProcWaitHandle,
            delegate {
                Exited = true;
                exitHandler?.Invoke();
            },
            null, -1, true
        );
    }

    public void Dispose() {
        if(!Exited) {
            var spin = new SpinWait();
            PInvoke.TerminateProcess(ProcInfo.hProcess, 0);
            while(!Exited) {
                spin.SpinOnce();
            }
        }

        ProcWaitRegistration.Unregister(null);
        ProcWaitHandle.Dispose();

        CloseProcHandles();
    }

    void CloseProcHandles() {
        NativeUtils.TryCloseHandle(ProcInfo.hProcess);
        NativeUtils.TryCloseHandle(ProcInfo.hThread);
        NativeUtils.TryCloseHandle(OutputReadPipe);
    }

    [SuppressMessage(
        "Reliability", "CA2000",
        Justification = "Safe handle is a wrapper required by FileStream, not the owner of the native handle")
    ]
    public Stream OpenOutput() {
        var safeHandle = new SafeFileHandle(OutputReadPipe, ownsHandle: false);
        return new FileStream(safeHandle, FileAccess.Read);
    }

    static unsafe HANDLE CreateAntiOrphanJobObject() {
        // On Windows, you can ensure that a child process terminates
        // when the parent process exits using a job object

        var job = PInvoke.CreateJobObject(default, default(PCWSTR));

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

        NativeUtils.MustSucceed(
            PInvoke.SetInformationJobObject(
                job,
                JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                Unsafe.AsPointer(ref info),
                (uint)Unsafe.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()
            )
        );

        return job;
    }

    class NativeWaitHandle : WaitHandle {
        public NativeWaitHandle(HANDLE nativeHandle) {
            SafeWaitHandle = new SafeWaitHandle(nativeHandle, false);
        }
    }
}
