#pragma once

// Minimal .net glue

#include <stddef.h>
#include <stdint.h>

// hostfxr_delegate_type (see hostfxr.h): hdt_load_assembly_and_get_function_pointer
#define DOTNET_HDT_LOAD_ASSEMBLY_AND_GET_FUNCTION_POINTER 5

// UNMANAGEDCALLERSONLY_METHOD, as defined in coreclr_delegates.h
#define DOTNET_UNMANAGEDCALLERSONLY_METHOD ((const char*)(uintptr_t)-1)

// Forward declaration of the flattened project struct (defined in bridge.h);
// only a pointer is ever passed around here.
struct bridge_yyp;

// int32_t compile_fn(const struct bridge_yyp* yyp, const char* project_dir, const char* output_path)
typedef int32_t (*compile_fn)(const struct bridge_yyp*, const char*, const char*);


// Locate libhostfxr.so, boot the runtime against the runtime config in
// `bridge_dir` (which must contain Bridge.runtimeconfig.json), and resolve
// BridgeLib.Bridge::Compile.

// Returns 0 on success. On failure returns a negative hostfxr_result-style code
// and writes a human readable explanation into `errbuf` (if provided).
int dotnet_init(const char* bridge_dir, char* errbuf, size_t errbuf_size);

// Returns the last error message produced by the hosting layer.
const char* dotnet_last_error(void);

// Invoke the already-resolved managed entry point. Returns its int status.
int32_t dotnet_invoke(const struct bridge_yyp* yyp, const char* project_dir, const char* output_path);

// Close the runtime host context, if any.
void dotnet_shutdown(void);