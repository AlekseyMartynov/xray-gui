using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Project.Tests;

public class NativeUtilsTests {
    public static readonly TheoryData<int> ErrorsToCheck = [
        (int)WIN32_ERROR.NO_ERROR,
        (int)WIN32_ERROR.ERROR_FILE_NOT_FOUND,
        (int)WIN32_ERROR.ERROR_ACCESS_DENIED,
    ];

    [Theory]
    [MemberData(nameof(ErrorsToCheck))]
    public void MustSucceed(int error) {
        CheckWin32Exception(error, true, delegate {
            SimulatePInvoke(error);
            NativeUtils.MustSucceed((BOOL)false);
        });
        CheckWin32Exception(error, false, delegate {
            NativeUtils.MustSucceed((WIN32_ERROR)error);
        });
        CheckWin32Exception(error, false, delegate {
            NativeUtils.MustSucceed((uint)error);
        });
    }

    [Theory]
    [MemberData(nameof(ErrorsToCheck))]
    public void CheckHandle(int error) {
        CheckWin32Exception(error, true, delegate {
            SimulatePInvoke(error);
            NativeUtils.CheckHandle(IntPtr.Zero);
        });
        CheckWin32Exception(error, true, delegate {
            SimulatePInvoke(error);
            unsafe {
                NativeUtils.CheckHandle(IntPtr.Zero.ToPointer());
            }
        });
    }

    static void SimulatePInvoke(int result) {
        Marshal.SetLastPInvokeError(result);
    }

    static void CheckWin32Exception(int expectedError, bool throwsOnNoError, Action testCode) {
        Marshal.SetLastPInvokeError(0);
        var x = Record.Exception(testCode);
        switch(expectedError) {
            case (int)WIN32_ERROR.NO_ERROR:
                if(throwsOnNoError) {
                    Assert.IsType<Win32Exception>(x);
                    Assert.Contains("completed successfully", x.Message);
                } else {
                    Assert.Null(x);
                }
                break;
            case (int)WIN32_ERROR.ERROR_FILE_NOT_FOUND:
                Assert.IsType<Win32Exception>(x);
                Assert.Contains("cannot find the file", x.Message);
                break;
            case (int)WIN32_ERROR.ERROR_ACCESS_DENIED:
                Assert.IsType<UIException>(x);
                Assert.Equal("Restart as Administrator", x.Message);
                break;
            default:
                throw new NotImplementedException();
        }
    }
}
