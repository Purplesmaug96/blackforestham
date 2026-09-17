#pragma once

// Bridge between C and C# for UndertaleModLib

typedef enum bfh_bridge_status {
    BFH_BRIDGE_OK = 0,
    BFH_BRIDGE_ERR_NOT_INITIALIZED = -1,
    BFH_BRIDGE_ERR_BRIDGE_NOT_FOUND = -2,
    BFH_BRIDGE_ERR_HOST_FAILED = -3,
    BFH_BRIDGE_ERR_ENTRY_POINT_MISSING = -4,
    BFH_BRIDGE_ERR_MANAGED_FAILED = -5,
} bfh_bridge_status_t;

bfh_bridge_status_t bfh_bridge_init();

bfh_bridge_status_t bfh_bridge_compile(const char* yyp_json, const char* project_dir, const char* output_path);

void bfh_bridge_shutdown();
