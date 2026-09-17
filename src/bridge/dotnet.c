#include "dotnet.h"

#include <dirent.h>
#include <dlfcn.h>
#include <limits.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

typedef struct hostfxr_handle_tag* hostfxr_handle;

typedef struct hostfxr_initialize_parameters {
    size_t size;
    const char* host_path;
    const char* dotnet_root;
} hostfxr_initialize_parameters;

typedef int32_t (*hostfxr_initialize_for_runtime_config_fn)(const char*, hostfxr_initialize_parameters*, hostfxr_handle*);
typedef int32_t (*hostfxr_get_runtime_delegate_fn)(hostfxr_handle, int32_t, void**);
typedef int32_t (*hostfxr_close_fn)(hostfxr_handle);
typedef int32_t (*get_function_pointer_fn)(const char*, const char*, const char*, const char*, void*, void**);

static void* g_hostfxr_lib = nullptr;
static hostfxr_handle g_hostfxr_ctx = nullptr;
static compile_fn g_compile = nullptr;
static char g_last_error[1024] = "";

static void set_error(const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    vsnprintf(g_last_error, sizeof(g_last_error), fmt, args);
    va_end(args);
}

// Copy a dlsym() result into a function-pointer slot without tripping over
// strict aliasing rules. Same size guarantees as POSIX.
static void load_symbol(void* lib, const char* name, void* fn_slot, size_t fn_size) {
    void* sym = dlsym(lib, name);
    if (sym == nullptr) {
        return;
    }
    memcpy(fn_slot, &sym, fn_size);
}

// Compare "1.2.3"-style versions numerically; >0 if a is newer.
static int version_compare(const char* a, const char* b) {
    // coreclr_delegates-style compares are overkill; a plain component-wise
    // strtoul walk is more than enough to pick the newest hostfxr.
    while (*a != '\0' || *b != '\0') {
        unsigned long na = 0, nb = 0;
        if (*a >= '0' && *a <= '9') {
            na = strtoul(a, (char**)&a, 10);
        }
        if (*b >= '0' && *b <= '9') {
            nb = strtoul(b, (char**)&b, 10);
        }
        if (na != nb) {
            return na > nb ? 1 : -1;
        }
        if (*a == '.') a++;
        if (*b == '.') b++;
    }
    return 0;
}

// If `root` is a .NET install dir, write the path to the newest libhostfxr.so
// found inside it into `out`. Returns 0 on success.
static int try_dotnet_root(const char* root, char* out, size_t out_size) {
    char fxr_dir[PATH_MAX];
    snprintf(fxr_dir, sizeof(fxr_dir), "%s/host/fxr", root);

    DIR* dir = opendir(fxr_dir);
    if (dir == nullptr) {
        return -1;
    }

    char best_name[NAME_MAX + 1] = "";
    char best_path[PATH_MAX] = "";
    const struct dirent* entry;
    while ((entry = readdir(dir)) != nullptr) {
        if (entry->d_name[0] == '.') {
            continue;
        }
        char candidate[PATH_MAX];
        snprintf(candidate, sizeof(candidate), "%s/%s/libhostfxr.so", fxr_dir, entry->d_name);
        if (access(candidate, R_OK) != 0) {
            continue;
        }
        if (best_name[0] == '\0' || version_compare(entry->d_name, best_name) > 0) {
            snprintf(best_name, sizeof(best_name), "%s", entry->d_name);
            snprintf(best_path, sizeof(best_path), "%s", candidate);
        }
    }
    closedir(dir);

    if (best_path[0] == '\0') {
        return -1;
    }
    snprintf(out, out_size, "%s", best_path);
    return 0;
}

// Shorten a resolved <root>/dotnet muxer path to just the install root.
static void strip_muxer(char* path) {
    char* slash = strrchr(path, '/');
    if (slash != nullptr) {
        *slash = '\0';
    }
}

static int find_libhostfxr(char* out, size_t out_size) {
    const char* candidate;

    if ((candidate = getenv("DOTNET_ROOT_X64")) != nullptr && try_dotnet_root(candidate, out, out_size) == 0) {
        return 0;
    }
    if ((candidate = getenv("DOTNET_ROOT")) != nullptr && try_dotnet_root(candidate, out, out_size) == 0) {
        return 0;
    }
    if ((candidate = getenv("DOTNET_ROOT_X86")) != nullptr && try_dotnet_root(candidate, out, out_size) == 0) {
        return 0;
    }

    // Derive the root from the `dotnet` muxer on PATH.
    char path_copy[PATH_MAX];
    const char* path = getenv("PATH");
    if (path != nullptr) {
        snprintf(path_copy, sizeof(path_copy), "%s", path);
        char resolved_muxer[PATH_MAX] = "";
        int found = 0;
        for (char* entry = strtok(path_copy, ":"); entry != nullptr && !found; entry = strtok(nullptr, ":")) {
            char muxer[PATH_MAX];
            snprintf(muxer, sizeof(muxer), "%s/dotnet", entry);
            if (access(muxer, X_OK) != 0) {
                continue;
            }
            if (realpath(muxer, resolved_muxer) == nullptr) {
                continue;
            }
            found = 1;
        }
        if (found) {
            strip_muxer(resolved_muxer);
            if (resolved_muxer[0] != '\0' && try_dotnet_root(resolved_muxer, out, out_size) == 0) {
                return 0;
            }
        }
    }

    // Well-known install locations.
    static const char* known_roots[] = {
        "/usr/share/dotnet",
        "/usr/lib/dotnet",
        "/usr/lib64/dotnet",
        "/opt/dotnet",
        nullptr,
    };
    for (size_t i = 0; known_roots[i] != nullptr; i++) {
        if (try_dotnet_root(known_roots[i], out, out_size) == 0) {
            return 0;
        }
    }

    set_error("could not locate libhostfxr.so (tried DOTNET_ROOT, `dotnet` on PATH, and common roots)");
    return -1;
}

// Turn "<root>/host/fxr/<version>/libhostfxr.so" back into "<root>".
static void dotnet_root_from_hostfxr(const char* hostfxr_path, char* out, size_t out_size) {
    // Walk backwards four components: the file name and host/fxr/<version>.
    const char* end = hostfxr_path + strlen(hostfxr_path);
    for (int i = 0; i < 4 && end > hostfxr_path; i++) {
        end--;
        while (end > hostfxr_path && *end != '/') end--;
    }
    snprintf(out, out_size, "%.*s", (int)(end - hostfxr_path), hostfxr_path);
}

int dotnet_init(const char* bridge_dir, char* errbuf, size_t errbuf_size) {
    char hostfxr_path[PATH_MAX];
    if (find_libhostfxr(hostfxr_path, sizeof(hostfxr_path)) != 0) {
        goto fail;
    }

    g_hostfxr_lib = dlopen(hostfxr_path, RTLD_NOW | RTLD_GLOBAL);
    if (g_hostfxr_lib == nullptr) {
        set_error("dlopen(%s) failed: %s", hostfxr_path, dlerror());
        goto fail;
    }

    hostfxr_initialize_for_runtime_config_fn init_runtime_config = nullptr;
    hostfxr_get_runtime_delegate_fn get_runtime_delegate = nullptr;
    hostfxr_close_fn close_fxr = nullptr;
    load_symbol(g_hostfxr_lib, "hostfxr_initialize_for_runtime_config", &init_runtime_config, sizeof(init_runtime_config));
    load_symbol(g_hostfxr_lib, "hostfxr_get_runtime_delegate", &get_runtime_delegate, sizeof(get_runtime_delegate));
    load_symbol(g_hostfxr_lib, "hostfxr_close", &close_fxr, sizeof(close_fxr));

    if (init_runtime_config == nullptr || get_runtime_delegate == nullptr || close_fxr == nullptr) {
        set_error("libhostfxr.so is missing a required export (dlsym: %s)", dlerror());
        goto fail;
    }

    char runtimeconfig[PATH_MAX];
    snprintf(runtimeconfig, sizeof(runtimeconfig), "%s/Bridge.runtimeconfig.json", bridge_dir);
    if (access(runtimeconfig, R_OK) != 0) {
        set_error("bridge is not published: %s does not exist", runtimeconfig);
        goto fail;
    }

    char dotnet_root[PATH_MAX];
    dotnet_root_from_hostfxr(hostfxr_path, dotnet_root, sizeof(dotnet_root));

    char assembly_path[PATH_MAX];
    snprintf(assembly_path, sizeof(assembly_path), "%s/Bridge.dll", bridge_dir);

    hostfxr_initialize_parameters init_params;
    init_params.size = sizeof(init_params);
    init_params.host_path = nullptr;
    init_params.dotnet_root = dotnet_root;

    int32_t result = init_runtime_config(runtimeconfig, &init_params, &g_hostfxr_ctx);
    if (result != 0) {
        set_error("hostfxr_initialize_for_runtime_config failed with 0x%X", result);
        goto fail;
    }

    get_function_pointer_fn get_function_pointer_fn_ptr = nullptr;
    result = get_runtime_delegate(g_hostfxr_ctx, DOTNET_HDT_LOAD_ASSEMBLY_AND_GET_FUNCTION_POINTER, (void**)&get_function_pointer_fn_ptr);
    if (result != 0 || get_function_pointer_fn_ptr == nullptr) {
        set_error("hostfxr_get_runtime_delegate(hdt_load_assembly_and_get_function_pointer) failed with 0x%X", result);
        goto fail;
    }

    void* entry_point = nullptr;
    result = get_function_pointer_fn_ptr(
        assembly_path,
        "BridgeLib.Bridge, Bridge",
        "Compile",
        DOTNET_UNMANAGEDCALLERSONLY_METHOD,
        nullptr,
        &entry_point);
    if (result != 0 || entry_point == nullptr) {
        set_error("load_assembly_and_get_function_pointer(BridgeLib.Bridge::Compile) failed with 0x%X", result);
        goto fail;
    }

    memcpy(&g_compile, &entry_point, sizeof(g_compile));

    if (errbuf != nullptr && errbuf_size > 0) {
        errbuf[0] = '\0';
    }
    return 0;

fail:
    if (errbuf != nullptr && errbuf_size > 0) {
        snprintf(errbuf, errbuf_size, "%s", g_last_error[0] != '\0' ? g_last_error : "unknown host error");
    }
    dotnet_shutdown();
    return -1;
}

const char* dotnet_last_error(void) {
    return g_last_error;
}

int32_t dotnet_invoke(const struct yyp* yyp, const char* project_dir, const char* output_path) {
    if (g_compile == nullptr) {
        set_error("managed entry point is not resolved; call bridge_init() first");
        return -1;
    }
    return g_compile(yyp, project_dir, output_path);
}

void dotnet_shutdown(void) {
    if (g_hostfxr_ctx != nullptr) {
        hostfxr_close_fn close_fxr = nullptr;
        if (g_hostfxr_lib != nullptr) {
            load_symbol(g_hostfxr_lib, "hostfxr_close", &close_fxr, sizeof(close_fxr));
        }
        if (close_fxr != nullptr) {
            close_fxr(g_hostfxr_ctx);
        }
        g_hostfxr_ctx = nullptr;
    }
    if (g_hostfxr_lib != nullptr) {
        dlclose(g_hostfxr_lib);
        g_hostfxr_lib = nullptr;
    }
    g_compile = nullptr;
}