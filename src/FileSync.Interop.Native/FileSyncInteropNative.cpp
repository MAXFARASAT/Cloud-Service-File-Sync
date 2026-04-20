#include <Windows.h>
#include <cfapi.h>
#include <string>
#include <fstream>

namespace
{
    CF_CONNECTION_KEY g_connectionKey = {};
    bool g_isConnected = false;

    void Log(const std::wstring& msg)
    {
        std::wofstream log(L"FileSyncInteropNative.log", std::ios::app);
        log << msg << L"\n";
    }

    void CALLBACK OnFetchData(
        const CF_CALLBACK_INFO* callbackInfo,
        const CF_CALLBACK_PARAMETERS* callbackParameters)
    {
        if (!callbackInfo || !callbackParameters)
        {
            return;
        }

        Log(L"OnFetchData callback invoked.");
        // Minimal prototype callback. Full data transfer handling is intentionally omitted.
        (void)callbackInfo;
        (void)callbackParameters;
    }

    HRESULT EnsureConnected(const wchar_t* syncRootPath)
    {
        if (g_isConnected)
        {
            return S_OK;
        }

        static const CF_CALLBACK_REGISTRATION callbacks[] =
        {
            { CF_CALLBACK_TYPE_FETCH_DATA, OnFetchData },
            CF_CALLBACK_REGISTRATION_END
        };

        const HRESULT hr = CfConnectSyncRoot(
            syncRootPath,
            callbacks,
            nullptr,
            CF_CONNECT_FLAG_NONE,
            &g_connectionKey);

        if (SUCCEEDED(hr))
        {
            g_isConnected = true;
            Log(L"CfConnectSyncRoot succeeded.");
        }
        else
        {
            Log(L"CfConnectSyncRoot failed.");
        }

        return hr;
    }
}

extern "C"
{
    __declspec(dllexport) HRESULT RegisterSyncRoot(const wchar_t* syncRootPath)
    {
        if (!syncRootPath || wcslen(syncRootPath) == 0)
        {
            return E_INVALIDARG;
        }

        Log(L"RegisterSyncRoot called.");

        // Minimal CFAPI prototype call path.
        // A production implementation should fill complete registration structures.
        CF_SYNC_REGISTRATION registration = {};
        registration.StructSize = sizeof(CF_SYNC_REGISTRATION);
        registration.ProviderName = L"FileSyncPrototype";
        registration.ProviderVersion = L"1.0";

        CF_SYNC_POLICIES policies = {};
        policies.StructSize = sizeof(CF_SYNC_POLICIES);
        policies.HardLink = CF_HARDLINK_POLICY_NONE;
        policies.Hydration.Primary = CF_HYDRATION_POLICY_PARTIAL;
        policies.InSync = CF_INSYNC_POLICY_TRACK_FILE_CREATION_TIME | CF_INSYNC_POLICY_TRACK_FILE_READONLY_ATTRIBUTE;
        policies.Population.Primary = CF_POPULATION_POLICY_PARTIAL;

        HRESULT hr = CfRegisterSyncRoot(syncRootPath, &registration, &policies, CF_REGISTER_FLAG_NONE);
        if (FAILED(hr))
        {
            Log(L"CfRegisterSyncRoot failed.");
            return hr;
        }

        return EnsureConnected(syncRootPath);
    }

    __declspec(dllexport) HRESULT UnregisterSyncRoot(const wchar_t* syncRootPath)
    {
        if (!syncRootPath || wcslen(syncRootPath) == 0)
        {
            return E_INVALIDARG;
        }

        Log(L"UnregisterSyncRoot called.");
        if (g_isConnected)
        {
            const HRESULT disconnectHr = CfDisconnectSyncRoot(g_connectionKey);
            if (FAILED(disconnectHr))
            {
                Log(L"CfDisconnectSyncRoot failed.");
            }
            else
            {
                g_isConnected = false;
                Log(L"CfDisconnectSyncRoot succeeded.");
            }
        }

        HRESULT hr = CfUnregisterSyncRoot(syncRootPath);
        if (FAILED(hr))
        {
            Log(L"CfUnregisterSyncRoot failed.");
        }

        return hr;
    }

    __declspec(dllexport) HRESULT CreatePlaceholderFile(const wchar_t* path, long long fileSize, int status)
    {
        if (!path || wcslen(path) == 0)
        {
            return E_INVALIDARG;
        }

        std::wstring p(path);
        std::wstring directory = p.substr(0, p.find_last_of(L"\\/"));
        std::wstring fileName = p.substr(p.find_last_of(L"\\/") + 1);
        FILETIME nowFileTime = {};
        GetSystemTimeAsFileTime(&nowFileTime);
        ULARGE_INTEGER nowTicks = {};
        nowTicks.LowPart = nowFileTime.dwLowDateTime;
        nowTicks.HighPart = nowFileTime.dwHighDateTime;
        LARGE_INTEGER now = {};
        now.QuadPart = static_cast<LONGLONG>(nowTicks.QuadPart);

        CF_PLACEHOLDER_CREATE_INFO info = {};
        info.RelativeFileName = fileName.c_str();
        info.FsMetadata.BasicInfo.FileAttributes = FILE_ATTRIBUTE_NORMAL;
        info.FsMetadata.BasicInfo.CreationTime = now;
        info.FsMetadata.BasicInfo.LastWriteTime = now;
        info.FsMetadata.BasicInfo.LastAccessTime = now;
        info.FsMetadata.BasicInfo.ChangeTime = now;
        info.FsMetadata.FileSize.QuadPart = fileSize;
        info.FileIdentity = fileName.c_str();
        info.FileIdentityLength = static_cast<DWORD>(fileName.size() * sizeof(wchar_t));
        info.Flags = CF_PLACEHOLDER_CREATE_FLAG_MARK_IN_SYNC;
        info.Result = S_OK;

        HRESULT hr = CfCreatePlaceholders(
            directory.c_str(),
            &info,
            1,
            CF_CREATE_FLAG_NONE,
            nullptr);

        (void)status;
        if (FAILED(hr))
        {
            Log(
                L"CfCreatePlaceholders failed. Hr=" + std::to_wstring(static_cast<unsigned long>(hr)) +
                L" ItemResult=" + std::to_wstring(static_cast<unsigned long>(info.Result)) +
                L" Path=" + p);
            return hr;
        }

        if (FAILED(info.Result))
        {
            Log(
                L"CfCreatePlaceholders item failed. ItemResult=" +
                std::to_wstring(static_cast<unsigned long>(info.Result)) +
                L" Path=" + p);
            return info.Result;
        }

        return hr;
    }

    __declspec(dllexport) HRESULT TriggerHydration(const wchar_t* path)
    {
        if (!path || wcslen(path) == 0)
        {
            return E_INVALIDARG;
        }

        const DWORD attrs = GetFileAttributesW(path);
        if (attrs == INVALID_FILE_ATTRIBUTES)
        {
            const DWORD lastError = GetLastError();
            Log(L"TriggerHydration path not accessible. LastError=" + std::to_wstring(lastError));
            return HRESULT_FROM_WIN32(lastError);
        }

        HANDLE fileHandle = INVALID_HANDLE_VALUE;
        DWORD lastError = ERROR_SUCCESS;

        auto tryOpen = [&](DWORD desiredAccess, DWORD flagsAndAttributes) -> bool
        {
            fileHandle = CreateFileW(
                path,
                desiredAccess,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                nullptr,
                OPEN_EXISTING,
                flagsAndAttributes,
                nullptr);

            if (fileHandle != INVALID_HANDLE_VALUE)
            {
                return true;
            }

            lastError = GetLastError();
            Log(
                L"TriggerHydration CreateFileW failed. Access=" + std::to_wstring(desiredAccess) +
                L" Flags=" + std::to_wstring(flagsAndAttributes) +
                L" LastError=" + std::to_wstring(lastError));
            return false;
        };

        const DWORD readAccess = FILE_READ_DATA | FILE_READ_ATTRIBUTES;
        const DWORD reparseFlag = FILE_FLAG_OPEN_REPARSE_POINT;

        if (!tryOpen(readAccess, reparseFlag) &&
            !tryOpen(readAccess, FILE_ATTRIBUTE_NORMAL) &&
            !tryOpen(0, reparseFlag) &&
            !tryOpen(0, FILE_ATTRIBUTE_NORMAL))
        {
            return HRESULT_FROM_WIN32(lastError);
        }

        LARGE_INTEGER offset = {};
        LARGE_INTEGER length = {};
        length.QuadPart = MAXLONGLONG;
        HRESULT hr = CfHydratePlaceholder(fileHandle, offset, length, CF_HYDRATE_FLAG_NONE, nullptr);
        if (FAILED(hr))
        {
            Log(L"CfHydratePlaceholder failed.");
        }
        CloseHandle(fileHandle);
        return hr;
    }

    __declspec(dllexport) HRESULT ConnectSyncRoot(const wchar_t* syncRootPath)
    {
        if (!syncRootPath || wcslen(syncRootPath) == 0)
        {
            return E_INVALIDARG;
        }

        Log(L"ConnectSyncRoot called.");
        return EnsureConnected(syncRootPath);
    }

    __declspec(dllexport) HRESULT DisconnectSyncRoot()
    {
        Log(L"DisconnectSyncRoot called.");
        if (!g_isConnected)
        {
            return S_OK;
        }

        const HRESULT hr = CfDisconnectSyncRoot(g_connectionKey);
        if (SUCCEEDED(hr))
        {
            g_isConnected = false;
            Log(L"CfDisconnectSyncRoot succeeded.");
        }
        else
        {
            Log(L"CfDisconnectSyncRoot failed.");
        }

        return hr;
    }

    __declspec(dllexport) HRESULT NotifyFileStateChange(const wchar_t* path, int status)
    {
        if (!path || wcslen(path) == 0)
        {
            return E_INVALIDARG;
        }

        (void)status;
        Log(L"NotifyFileStateChange called.");
        // Prototype hook for propagating file state transitions to native engine.
        return S_OK;
    }
}
