#include "bfh_bridge.h"

#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

#include "bfh_dotnet.h"

#ifndef BFH_BRIDGE_DEFAULT_DIR
#define BFH_BRIDGE_DEFAULT_DIR ""
#endif

static int g_initialized = 0;

// Resolve the directory the published managed bridge lives in, preferring:
//   1. BFH_BRIDGE_DIR
//   2. BFH_BRIDGE_DEFAULT_DIR (set by CMake to the dotnet publish output dir)
//   3. a "bridge" subdirectory next to the currently running executable
static int find_bridge_dir(char* out, size_t out_size) {
    const char* env_dir = getenv("BFH_BRIDGE_DIR");
    if (env_dir != nullptr && env_dir[0] != '\0') {
        snprintf(out, out_size, "%s", env_dir);
        return 0;
    }

    if (BFH_BRIDGE_DEFAULT_DIR[0] != '\0') {
        snprintf(out, out_size, "%s", BFH_BRIDGE_DEFAULT_DIR);
        return 0;
    }

#if defined(__linux__)
    char exe_path[PATH_MAX];
    ssize_t len = readlink("/proc/self/exe", exe_path, sizeof(exe_path) - 1);
    if (len > 0) {
        exe_path[len] = '\0';
        char* slash = strrchr(exe_path, '/');
        if (slash != nullptr) {
            *slash = '\0';
            snprintf(out, out_size, "%s/bridge", exe_path);
            return 0;
        }
    }
#endif

    return -1;
}

bfh_bridge_status_t bfh_bridge_init() {
    if (g_initialized) {
        return BFH_BRIDGE_OK;
    }

    char bridge_dir[PATH_MAX];
    if (find_bridge_dir(bridge_dir, sizeof(bridge_dir)) != 0) {
        fprintf(stderr, "bfh_bridge: unable to locate the bridge directory\n");
        return BFH_BRIDGE_ERR_BRIDGE_NOT_FOUND;
    }

    char errbuf[1024] = "";
    if (bfh_dotnet_init(bridge_dir, errbuf, sizeof(errbuf)) != 0) {
        fprintf(stderr, "bfh_bridge: failed to host the .NET runtime: %s\n", errbuf[0] != '\0' ? errbuf : bfh_dotnet_last_error());
        return BFH_BRIDGE_ERR_HOST_FAILED;
    }

    g_initialized = 1;
    return BFH_BRIDGE_OK;
}

bfh_bridge_status_t bfh_bridge_compile(const char* yyp_json, const char* project_dir, const char* output_path) {
    if (!g_initialized) {
        fprintf(stderr, "bfh_bridge: bfh_bridge_init() was not called\n");
        return BFH_BRIDGE_ERR_NOT_INITIALIZED;
    }

    int32_t status = bfh_dotnet_invoke(yyp_json, project_dir, output_path);
    if (status != 0) {
        fprintf(stderr, "bfh_bridge: managed side returned %d\n", status);
        return BFH_BRIDGE_ERR_MANAGED_FAILED;
    }
    return BFH_BRIDGE_OK;
}

void bfh_bridge_shutdown() {
    if (g_initialized) {
        bfh_dotnet_shutdown();
        g_initialized = 0;
    }
}
