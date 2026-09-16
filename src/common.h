#pragma once
#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifndef nullptr
#define nullptr NULL
#endif

static inline void* safe_malloc_function(size_t size, char* file, int line) {
    if (size == 0) { return nullptr; }

    void* ptr = malloc(size);
    if (ptr == nullptr) {
        fprintf(stderr, "Failed to allocate %zu bytes of memory at %s:%d\n", size, file, line);
        abort();
    }
    memset(ptr, 0, size);

    return ptr;
}
#define safe_malloc(size) safe_malloc_function(size, __FILE__, __LINE__)

static inline void* safe_calloc_function(size_t num, size_t size, char* file, int line) {
    if (num == 0 || size == 0) { return nullptr; }

    void* ptr = calloc(num, size);
    if (ptr == nullptr) {
        fprintf(stderr, "Failed to allocate %zu bytes of memory at %s:%d\n", num * size, file, line);
        abort();
    }

    return ptr;
}
#define safe_calloc(num, size) safe_calloc_function(num, size, __FILE__, __LINE__)

