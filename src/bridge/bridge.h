#pragma once

// Bridge between C and C# for UndertaleModLib

#include <stdint.h>

#include "yyp.h"

typedef enum bridge_status {
    BRIDGE_OK = 0,
    BRIDGE_ERR_NOT_INITIALIZED = -1,
    BRIDGE_ERR_BRIDGE_NOT_FOUND = -2,
    BRIDGE_ERR_HOST_FAILED = -3,
    BRIDGE_ERR_ENTRY_POINT_MISSING = -4,
    BRIDGE_ERR_MANAGED_FAILED = -5,
} bridge_status_t;

// Flattened, marshallable view of a parsed yyp_t, passed across the bridge as
// a single struct pointer. All strings are NUL-terminated UTF-8 pointers
// into yyp_t-owned memory; they are only valid for the duration of the call.
// Mirrored by the BridgeYyp managed struct in Bridge.cs ([StructLayout] +
// pointer fields). Keep field order and types in lockstep.
typedef struct bridge_yyp {
    // %Name / displayName of the project
    const char* name;
    const char* display_name;

    // folder names (%Name of each item in "Folders"), in project order
    int32_t folder_count;
    const char** folder_names;

    // "resources" (name + path of each id), in project order
    int32_t resource_count;
    const char** resource_names;
    const char** resource_paths;
} bridge_yyp_t;

bridge_status_t bridge_init();

// Compile a parsed project. The flattened contents of `yyp` are handed to the
// managed bridge, which consumes them directly (no JSON round-trip).
bridge_status_t bridge_compile(const bridge_yyp_t* yyp, const char* project_dir, const char* output_path);

// Build a flattened bridge_yyp_t from a parsed yyp_t and compile it. The flattened
// view borrows strings from `yyp` and is only valid for the duration of the call.
bridge_status_t bridge_compile_yyp(yyp_t* yyp, const char* project_dir, const char* output_path);

void bridge_shutdown();