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

bridge_status_t bridge_init();

// Compile a parsed project to `output_path`. The managed side receives a
// pointer to the real yyp_t (and the room/folder/resource structs it owns) and
// mirrors those layouts directly; there is no flattened copy.
bridge_status_t bridge_compile(const yyp_t* yyp, const char* project_dir, const char* output_path);

void bridge_shutdown();
