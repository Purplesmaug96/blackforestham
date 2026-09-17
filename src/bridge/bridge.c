#include "bridge.h"

#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

#include "dotnet.h"

#ifndef BRIDGE_DEFAULT_DIR
#define BRIDGE_DEFAULT_DIR ""
#endif

static int g_initialized = 0;

// Resolve the directory the published managed bridge lives in, preferring:
//   1. BRIDGE_DIR
//   2. BRIDGE_DEFAULT_DIR (set by CMake to the dotnet publish output dir)
//   3. a "bridge" subdirectory next to the currently running executable
static int find_bridge_dir(char* out, size_t out_size) {
    const char* env_dir = getenv("BRIDGE_DIR");
    if (env_dir != nullptr && env_dir[0] != '\0') {
        snprintf(out, out_size, "%s", env_dir);
        return 0;
    }

    if (BRIDGE_DEFAULT_DIR[0] != '\0') {
        snprintf(out, out_size, "%s", BRIDGE_DEFAULT_DIR);
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

bridge_status_t bridge_init() {
    if (g_initialized) {
        return BRIDGE_OK;
    }

    char bridge_dir[PATH_MAX];
    if (find_bridge_dir(bridge_dir, sizeof(bridge_dir)) != 0) {
        fprintf(stderr, "bridge: unable to locate the bridge directory\n");
        return BRIDGE_ERR_BRIDGE_NOT_FOUND;
    }

    char errbuf[1024] = "";
    if (dotnet_init(bridge_dir, errbuf, sizeof(errbuf)) != 0) {
        fprintf(stderr, "bridge: failed to host the .NET runtime: %s\n", errbuf[0] != '\0' ? errbuf : dotnet_last_error());
        return BRIDGE_ERR_HOST_FAILED;
    }

    g_initialized = 1;
    return BRIDGE_OK;
}

bridge_status_t bridge_compile(const bridge_yyp_t* yyp, const char* project_dir, const char* output_path) {
    if (!g_initialized) {
        fprintf(stderr, "bridge: bridge_init() was not called\n");
        return BRIDGE_ERR_NOT_INITIALIZED;
    }

    int32_t status = dotnet_invoke(yyp, project_dir, output_path);
    if (status != 0) {
        fprintf(stderr, "bridge: managed side returned %d\n", status);
        return BRIDGE_ERR_MANAGED_FAILED;
    }
    return BRIDGE_OK;
}

void bridge_shutdown() {
    if (g_initialized) {
        dotnet_shutdown();
        g_initialized = 0;
    }
}