shader_type canvas_item;

uniform sampler2D frame_texture : filter_linear, repeat_disable;
uniform vec4 frame_region;
uniform vec4 portrait_region;

void fragment() {
    // NCard places Portrait (-125,-168,250,190) over Frame (-150,-211,300,422).
    // UV is in atlas coordinates; convert it to portrait-local coordinates before sampling the frame.
    vec2 portrait_uv = (UV - portrait_region.xy) / portrait_region.zw;
    vec2 frame_uv = (vec2(25.0, 43.0) + portrait_uv * vec2(250.0, 190.0)) / vec2(300.0, 422.0);
    vec2 mask_uv = frame_region.xy + frame_uv * frame_region.zw;
    COLOR.a *= 1.0 - texture(frame_texture, mask_uv).a;
}
