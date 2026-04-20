using System.Runtime.InteropServices;

namespace FileSync.Core;

public sealed class NativeSyncException : Exception
{
    public NativeSyncException(string message, int hresult) : base($"{message} (0x{hresult:X8})")
    {
        HResult = hresult;
    }
}

public static class NativeSyncInterop
{
    private const string DllName = "FileSyncInteropNative";

    [DllImport(DllName, CharSet = CharSet.Unicode)]
    private static extern int RegisterSyncRoot(string syncRootPath);

    [DllImport(DllName, CharSet = CharSet.Unicode)]
    private static extern int ConnectSyncRoot(string syncRootPath);

    [DllImport(DllName, CharSet = CharSet.Unicode)]
    private static extern int DisconnectSyncRoot();

    [DllImport(DllName, CharSet = CharSet.Unicode)]
    private static extern int UnregisterSyncRoot(string syncRootPath);

    [DllImport(DllName, CharSet = CharSet.Unicode)]
    private static extern int CreatePlaceholderFile(string path, long fileSize, int status);

    [DllImport(DllName, CharSet = CharSet.Unicode)]
    private static extern int TriggerHydration(string path);

    [DllImport(DllName, CharSet = CharSet.Unicode)]
    private static extern int NotifyFileStateChange(string path, int status);

    public static void RegisterRootOrThrow(string syncRootPath) =>
        ThrowOnError(RegisterSyncRoot(syncRootPath), "CFAPI sync root registration failed");

    public static void ConnectRootOrThrow(string syncRootPath) =>
        ThrowOnError(ConnectSyncRoot(syncRootPath), "CFAPI sync root connect failed");

    public static void DisconnectRootOrThrow() =>
        ThrowOnError(DisconnectSyncRoot(), "CFAPI sync root disconnect failed");

    public static void UnregisterRootOrThrow(string syncRootPath) =>
        ThrowOnError(UnregisterSyncRoot(syncRootPath), "CFAPI sync root unregistration failed");

    public static void CreatePlaceholderOrThrow(string filePath, long size, SyncStatus status) =>
        ThrowOnError(CreatePlaceholderFile(filePath, size, (int)status), "CFAPI placeholder creation failed");

    public static void TriggerHydrationOrThrow(string filePath) =>
        ThrowOnError(TriggerHydration(filePath), "CFAPI hydration trigger failed");

    public static void NotifyStateOrThrow(string filePath, SyncStatus status) =>
        ThrowOnError(NotifyFileStateChange(filePath, (int)status), "CFAPI state notification failed");

    private static void ThrowOnError(int hr, string message)
    {
        if (hr != 0)
        {
            throw new NativeSyncException(message, hr);
        }
    }
}
