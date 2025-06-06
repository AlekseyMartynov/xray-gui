namespace Project.Tests;

public sealed class NativeProcessTests : IDisposable {
    static readonly string TestCmdPath = Path.Join(AppContext.BaseDirectory, "process_test.cmd");
    static readonly string TestOutputPath = Path.Join(AppContext.BaseDirectory, "process_test.txt");

    readonly TaskCompletionSource ExitTCS = new();

    public NativeProcessTests() {
        File.WriteAllText(
            TestCmdPath,
            "echo %* > " + TestOutputPath.Quote()
        );
    }

    public void Dispose() {
        File.Delete(TestCmdPath);
    }

    void ExitHandler() {
        ExitTCS.SetResult();
    }

    [Fact]
    public async Task Default() {
        using var proc = new NativeProcess(
            "cmd.exe /c " + TestCmdPath.Quote() + " test123",
            exitHandler: ExitHandler
        );

        await ExitTCS.Task;

        Assert.Equal("test123", ReadTestOutput());
    }

    [Fact]
    public async Task Env() {
        using var proc = new NativeProcess(
            "cmd.exe /c " + TestCmdPath.Quote() + " %TEST_123% %TEST_124%",
            env: [
                "TEST_123=123",
                "TEST_124=124"
            ],
            exitHandler: ExitHandler
        );

        await ExitTCS.Task;

        Assert.Equal("123 124", ReadTestOutput());
    }

    [Fact]
    public async Task DisposeNonExited() {
        var proc = new NativeProcess(
            "cmd.exe /c timeout /T -1",
            exitHandler: ExitHandler
        );

        proc.Dispose();

        await ExitTCS.Task;
    }

    static string ReadTestOutput() {
        return File.ReadAllText(TestOutputPath).TrimEnd();
    }
}
