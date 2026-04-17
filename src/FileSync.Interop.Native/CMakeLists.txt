cmake_minimum_required(VERSION 3.20)
project(FileSyncInteropNative LANGUAGES CXX)

add_library(FileSyncInteropNative SHARED
    FileSyncInteropNative.cpp
)

target_compile_features(FileSyncInteropNative PRIVATE cxx_std_17)
target_compile_definitions(FileSyncInteropNative PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN)
target_link_libraries(FileSyncInteropNative PRIVATE CldApi)
