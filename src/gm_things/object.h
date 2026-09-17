#pragma once

#include <json-c/json_types.h>
#include <stdint.h>

// Object structures parsed from a GMObject .yy file.
//
// Everything here is deliberately laid out as plain, pointer-based data so the
// exact same struct can be mirrored field-for-field on the C# side of the
// bridge (see Bridge.cs). Keep field order and types in lockstep with the C#
// mirrors; no flattening/repacking step exists.

// Mirrors UndertaleModLib's CollisionShapeFlags.
typedef enum {
    GM_OBJECT_COLLISION_CIRCLE = 0,
    GM_OBJECT_COLLISION_BOX = 1,
    GM_OBJECT_COLLISION_CUSTOM = 2,
} gm_object_collision_shape_t;

// Mirrors UndertaleModLib's EventType.
typedef enum {
    GM_OBJECT_EVENT_CREATE = 0,
    GM_OBJECT_EVENT_DESTROY = 1,
    GM_OBJECT_EVENT_ALARM = 2,
    GM_OBJECT_EVENT_STEP = 3,
    GM_OBJECT_EVENT_COLLISION = 4,
    GM_OBJECT_EVENT_KEYBOARD = 5,
    GM_OBJECT_EVENT_MOUSE = 6,
    GM_OBJECT_EVENT_OTHER = 7,
    GM_OBJECT_EVENT_DRAW = 8,
    GM_OBJECT_EVENT_KEYPRESS = 9,
    GM_OBJECT_EVENT_KEYRELEASE = 10,
    GM_OBJECT_EVENT_TRIGGER = 11,
    GM_OBJECT_EVENT_CLEANUP = 12,
    GM_OBJECT_EVENT_GESTURE = 13,
    GM_OBJECT_EVENT_PRECREATE = 14,
} gm_object_event_type_t;

// Mirrors UndertaleGameObject.UndertalePhysicsVertex (physicsShapePoints entry).
typedef struct {
    float x;
    float y;
} gm_object_vertex_t;

// Mirrors UndertaleGameObject.Event. `file` is the project-relative .gml path
// derived from the event's type/subtype (NULL when the event type is unknown).
// For collision events `collision` holds collisionObjectId.name.
typedef struct {
    int32_t type;      // gm_object_event_type_t
    int32_t num;       // eventNum / subtype
    char* collision;   // collisionObjectId.name, NULL when absent
    char* file;        // project-relative .gml path, NULL when unresolvable
} gm_object_event_t;

// Mirrors UndertaleGameObject. Computed values (event .gml paths) are resolved
// by the parser so the bridge stays a dumb mapper.
typedef struct {
    char* name;
    char* sprite_name;       // spriteId.name, NULL when absent
    char* sprite_mask_name;  // spriteMaskId.name, NULL when absent
    char* parent_name;       // parentObjectId.name, NULL when absent
    int32_t visible;
    int32_t solid;
    int32_t persistent;
    int32_t depth;
    int32_t uses_physics;
    int32_t is_sensor;
    int32_t collision_shape;
    float density;
    float restitution;
    float linear_damping;
    float angular_damping;
    float friction;
    uint32_t group;
    int32_t awake;
    int32_t kinematic;
    int32_t vertex_count;
    gm_object_vertex_t* vertices;
    int32_t event_count;
    gm_object_event_t* events;
} gm_object_t;

// `resource_path` is the project-relative path of the object's own .yy (e.g.
// "objects/foo/foo.yy"); it is used to locate the sibling event .gml files.
gm_object_t* gm_object_parse(json_object* obj, const char* resource_path);

void gm_object_free(gm_object_t* object);
