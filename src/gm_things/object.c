#include "object.h"

#include <stdio.h>
#include <string.h>

#include "common.h"
#include "_json_helpers.h"

// Read the "name" out of a {name, path} resource reference, or NULL when the
// reference is missing/null (e.g. "spriteId": null).
static char* ref_name(json_object* obj, const char* name) {
    json_object* ref = get_field(obj, name);
    if (ref == nullptr || json_object_is_type(ref, json_type_null)) {
        return nullptr;
    }
    return get_obj_string(get_field(ref, "name"));
}

// Directory part of a project-relative resource path (e.g.
// "objects/foo/foo.yy" -> "objects/foo"), or "" when there is no slash.
static char* dir_of(const char* path) {
    const char* slash = strrchr(path, '/');
    if (slash == nullptr) {
        return strdup("");
    }
    size_t len = (size_t)(slash - path);
    char* dir = safe_malloc(len + 1);
    memcpy(dir, path, len);
    dir[len] = '\0';
    return dir;
}

// GameMaker names event .gml files "<Prefix>_<subtype>.gml", except collision
// events which are "<Collision>_<otherObjectName>.gml".
static const char* event_prefix(int32_t type) {
    switch (type) {
        case GM_OBJECT_EVENT_CREATE: return "Create";
        case GM_OBJECT_EVENT_DESTROY: return "Destroy";
        case GM_OBJECT_EVENT_ALARM: return "Alarm";
        case GM_OBJECT_EVENT_STEP: return "Step";
        case GM_OBJECT_EVENT_KEYBOARD: return "Keyboard";
        case GM_OBJECT_EVENT_MOUSE: return "Mouse";
        case GM_OBJECT_EVENT_OTHER: return "Other";
        case GM_OBJECT_EVENT_DRAW: return "Draw";
        case GM_OBJECT_EVENT_KEYPRESS: return "KeyPress";
        case GM_OBJECT_EVENT_KEYRELEASE: return "KeyRelease";
        case GM_OBJECT_EVENT_TRIGGER: return "Trigger";
        case GM_OBJECT_EVENT_CLEANUP: return "CleanUp";
        case GM_OBJECT_EVENT_GESTURE: return "Gesture";
        case GM_OBJECT_EVENT_PRECREATE: return "PreCreate";
        default: return nullptr;
    }
}

// Returns 0 on success, -1 when the event cannot be mapped to a project file.
static int parse_event(gm_object_event_t* event, json_object* obj, const char* dir) {
    event->type = get_obj_int(obj, "eventType");
    event->num = get_obj_int(obj, "eventNum");
    event->collision = ref_name(obj, "collisionObjectId");
    event->file = nullptr;

    if (event->type == GM_OBJECT_EVENT_COLLISION) {
        if (event->collision == nullptr) {
            fprintf(stderr, "object: collision event without collisionObjectId\n");
            return -1;
        }
        size_t size = strlen(dir) + strlen("/Collision_.gml") + strlen(event->collision) + 1;
        event->file = safe_malloc(size);
        snprintf(event->file, size, "%s/Collision_%s.gml", dir, event->collision);
        return 0;
    }

    const char* prefix = event_prefix(event->type);
    if (prefix == nullptr) {
        fprintf(stderr, "object: unknown event type %d\n", event->type);
        free(event->collision);
        event->collision = nullptr;
        return -1;
    }

    size_t size = strlen(dir) + strlen("/_.gml") + strlen(prefix) + 16;
    event->file = safe_malloc(size);
    snprintf(event->file, size, "%s/%s_%d.gml", dir, prefix, event->num);
    return 0;
}

gm_object_t* gm_object_parse(json_object* obj, const char* resource_path) {
    gm_object_t* object = safe_malloc(sizeof(gm_object_t));
    object->name = get_obj_string(get_field(obj, "%Name"));
    object->sprite_name = ref_name(obj, "spriteId");
    object->sprite_mask_name = ref_name(obj, "spriteMaskId");
    object->parent_name = ref_name(obj, "parentObjectId");
    object->visible = get_obj_bool(obj, "visible");
    object->solid = get_obj_bool(obj, "solid");
    object->persistent = get_obj_bool(obj, "persistent");
    object->depth = get_obj_int(obj, "depth");

    object->uses_physics = get_obj_bool(obj, "physicsObject");
    object->is_sensor = get_obj_bool(obj, "physicsSensor");
    object->collision_shape = get_obj_int(obj, "physicsShape");
    object->density = get_obj_float(obj, "physicsDensity");
    object->restitution = get_obj_float(obj, "physicsRestitution");
    object->linear_damping = get_obj_float(obj, "physicsLinearDamping");
    object->angular_damping = get_obj_float(obj, "physicsAngularDamping");
    object->friction = get_obj_float(obj, "physicsFriction");
    object->group = get_obj_uint(obj, "physicsGroup");
    object->awake = get_obj_bool(obj, "physicsStartAwake");
    object->kinematic = get_obj_bool(obj, "physicsKinematic");

    json_object* points = get_field(obj, "physicsShapePoints");
    int32_t point_count = points ? (int32_t)json_object_array_length(points) : 0;
    object->vertex_count = point_count;
    object->vertices = safe_calloc((size_t)point_count, sizeof(gm_object_vertex_t));
    for (int32_t i = 0; i < point_count; i++) {
        json_object* point = json_object_array_get_idx(points, i);
        object->vertices[i].x = get_obj_float(point, "x");
        object->vertices[i].y = get_obj_float(point, "y");
    }

    char* dir = dir_of(resource_path);
    json_object* events = get_field(obj, "eventList");
    int32_t capacity = events ? (int32_t)json_object_array_length(events) : 0;
    object->events = safe_calloc((size_t)capacity, sizeof(gm_object_event_t));
    object->event_count = 0;
    for (int32_t i = 0; i < capacity; i++) {
        gm_object_event_t candidate;
        if (parse_event(&candidate, json_object_array_get_idx(events, i), dir) == 0) {
            object->events[object->event_count++] = candidate;
        }
    }
    free(dir);

    return object;
}

void gm_object_free(gm_object_t* object) {
    if (object == nullptr) {
        return;
    }

    free(object->name);
    free(object->sprite_name);
    free(object->sprite_mask_name);
    free(object->parent_name);
    free(object->vertices);

    for (int32_t i = 0; i < object->event_count; i++) {
        free(object->events[i].collision);
        free(object->events[i].file);
    }
    free(object->events);

    free(object);
}
