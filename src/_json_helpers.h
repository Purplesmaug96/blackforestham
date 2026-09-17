// For internal use only
#pragma once

#include <json-c/json.h>
#include <json-c/json_object.h>
#include <json-c/json_object_iterator.h>
#include <string.h>

typedef struct {
    int len;
    json_object* obj;
} _json_array;

static inline json_object* get_field(json_object* obj, char* name) {
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
