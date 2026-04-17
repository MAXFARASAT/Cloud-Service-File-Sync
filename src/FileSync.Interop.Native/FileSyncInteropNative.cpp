#include <Windows.h>
#include <cfapi.h>
#include <string>
#include <fstream>

namespace
{
    void Log(const std::wstring& msg)
    {
        std::wofstream log(L"FileSyncInteropNative.log", std::ios::app);
        log << msg << L"\n";
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

        CF_PLACEHOLDER_CREATE_INFO info = {};
        info.RelativeFileName = fileName.c_str();
        info.FsMetadata.BasicInfo.FileAttributes = FILE_ATTRIBUTE_ARCHIVE;
        info.FsMetadata.FileSize.QuadPart = fileSize;
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
            Log(L"CfCreatePlaceholders failed.");
        }

        return hr;
    }

    __declspec(dllexport) HRESULT TriggerHydration(const wchar_t* path)
    {
        if (!path || wcslen(path) == 0)
        {
            return E_INVALIDARG;
        }

        HANDLE fileHandle = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, nullptr);
        if (fileHandle == INVALID_HANDLE_VALUE)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        LARGE_INTEGER offset = {};
        LARGE_INTEGER length = {};
        length.QuadPart = MAXLONGLONG;
        HRESULT hr = CfHydratePlaceholder(fileHandle, offset, length, CF_HYDRATE_FLAG_NONE, nullptr);
        CloseHandle(fileHandle);
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
