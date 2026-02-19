using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.JobObjects;
using Windows.Win32.System.Threading;

namespace Project;

class NativeProcess : IDisposable {
    static readonly HANDLE AntiOrphanJobObject = CreateAntiOrphanJobObject();

    readonly PROCESS_INFORMATION ProcInfo;

    readonly NativeWaitHandle ProcWaitHandle;
    readonly RegisteredWaitHandle ProcWaitRegistration;

    bool Exited;

    public unsafe NativeProcess(string commandLine, string? workDir = null, string[]? env = null, Action? exitHandler = null, HANDLE accessToken = default) {
        var si = new STARTUPINFOW {
            cb = (uint)Unsafe.SizeOf<STARTUPINFOW>(),
        };

        var flags = PROCESS_CREATION_FLAGS.CREATE_SUSPENDED | PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;

        if(AppConfig.ProcConsole) {
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

        fixed(void* envBufPtr = envBuf) {
            NativeUtils.MustSucceed(
                PInvoke.CreateProcessAsUser(
                    accessToken,
                    default,
                    ref commandLineSpan,
                    default, default, default,
                    flags,
                    envBufPtr,
                    workDir,
                    in si,
                    out ProcInfo
                )
            );
        }

        var proc = ProcInfo.hProcess;

        NativeUtils.MustSucceed(PInvoke.AssignProcessToJobObject(AntiOrphanJobObject, proc));

        if(PInvoke.ResumeThread(ProcInfo.hThread) != 1) {
            throw new InvalidOperationException();
        }

        ProcWaitHandle = new NativeWaitHandle(proc);

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

        PInvoke.CloseHandle(ProcInfo.hProcess);
        PInvoke.CloseHandle(ProcInfo.hThread);
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
