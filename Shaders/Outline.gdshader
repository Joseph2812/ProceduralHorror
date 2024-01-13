shader_type spatial;
render_mode cull_front, unshaded;

uniform float outline_width : hint_range(0.0, 10.0) = 0.0;
uniform vec4 outline_colour : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void vertex() {
	if (outline_width > 0.0 && outline_colour.a > 0.0) {
		vec4 clip_position = PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0));
		vec3 clip_normal = mat3(PROJECTION_MATRIX) * (mat3(MODELVIEW_MATRIX) * NORMAL);
	
		vec2 offset = normalize(clip_normal.xy) / VIEWPORT_SIZE * clip_position.w * outline_width * 2.0;
		clip_position.xy += offset;
	
		POSITION = clip_position;
	}
}

void fragment() {
	ALBEDO = outline_colour.rgb;
	if (outline_colour.a < 1.0) { ALPHA = outline_colour.a; }
}