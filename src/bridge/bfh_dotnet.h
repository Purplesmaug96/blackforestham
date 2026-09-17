#pragma once

// Minimal .net glue

#include <stddef.h>
#include <stdint.h>

// hostfxr_delegate_type (see hostfxr.h): hdt_load_assembly_and_get_function_pointer
#define BFH_HDT_LOAD_ASSEMBLY_AND_GET_FUNCTION_POINTER 5

// UNMANAGEDCALLERSONLY_METHOD, as defined in coreclr_delegates.h
#define BFH_UNMANAGEDCALLERSONLY_METHOD ((const char*)(uintptr_t)-1)

// int32_t bfh_compile_fn(const char* yyp_json, const char* project_dir, const char* output_path)
typedef int32_t (*bfh_compile_fn)(const char*, const char*, const char*);


// Locate libhostfxr.so, boot the runtime against the runtime config in
// `bridge_dir` (which must contain BfhBridge.runtimeconfig.json), and resolve
// BfhBridgeLib.BfhBridge::Bfh_Compile.

// Returns 0 on success. On failure returns a negative hostfxr_result-style code
// and writes a human readable explanation into `errbuf` (if provided).
int bfh_dotnet_init(const char* bridge_dir, char* errbuf, size_t errbuf_size);

// Returns the last error message produced by the hosting layer.
const char* bfh_dotnet_last_error(void);

// Invoke the already-resolved managed entry point. Returns its int status.
int32_t bfh_dotnet_invoke(const char* yyp_json, const char* project_dir, const char* output_path);

// Close the runtime host context, if any.
void bfh_dotnet_shutdown(void);