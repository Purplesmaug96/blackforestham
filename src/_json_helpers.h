// For internal use only
#pragma once

#include <json-c/json.h>
#include <json-c/json_object.h>
#include <json-c/json_object_iterator.h>
#include <stdint.h>
#include <string.h>

typedef struct {
    int len;
    json_object* obj;
} _json_array;

static inline json_object* get_field(json_object* obj, const char* name) {
    json_object* field;
    json_object_object_get_ex(obj, name, &field);
    return field;
}

static inline char* get_obj_string(json_object* obj) {
    return obj ? strdup(json_object_get_string(obj)) : nullptr;
}

static inline _json_array get_obj_array(json_object* obj) {
    _json_array array = { 0 };
    array.len = json_object_array_length(obj);
    array.obj = obj;
    return array;
}

// Typed field accessors. Named get_obj_* rather than json_* so they never clash
// with json-c's own symbols.

static inline int32_t get_obj_int(json_object* obj, const char* name) {
    json_object* field = get_field(obj, name);
    return field ? (int32_t)json_object_get_int64(field) : 0;
}

static inline uint32_t get_obj_uint(json_object* obj, const char* name) {
    json_object* field = get_field(obj, name);
    return field ? (uint32_t)json_object_get_int64(field) : 0;
}

static inline float get_obj_float(json_object* obj, const char* name) {
    json_object* field = get_field(obj, name);
    return field ? (float)json_object_get_double(field) : 0.0f;
}

static inline int32_t get_obj_bool(json_object* obj, const char* name) {
    json_object* field = get_field(obj, name);
    return field ? (json_object_get_boolean(field) ? 1 : 0) : 0;
}
