shader_type spatial;

uniform sampler2D albedo_map_1    : source_color;
uniform sampler2D roughness_map_1 : hint_default_white;
uniform sampler2D metallic_map_1  : hint_default_black;
uniform sampler2D normal_map_1    : hint_normal;
uniform sampler2D ao_map_1        : hint_default_white;

uniform sampler2D albedo_map_2    : source_color;
uniform sampler2D roughness_map_2 : hint_default_white;
uniform sampler2D metallic_map_2  : hint_default_black;
uniform sampler2D normal_map_2    : hint_normal;
uniform sampler2D ao_map_2        : hint_default_white;

const float ONE_THIRD = 1.0 / 3.0;
const vec2 UV_SCALE = vec2(3.0, 2.0);

// Only allow textures at their specified UV areas (for Godot's BoxMesh)
bool is_side2(vec2 uv){
	return
	(
		(uv.x > 0.5 * ONE_THIRD && uv.x < 2.5 * ONE_THIRD && uv.y < 0.5) ||
		(
			(
				(uv.x > ONE_THIRD && uv.x < 1.5 * ONE_THIRD) ||
				(uv.x > 2.5 * ONE_THIRD)
			) &&
			uv.y > 0.5
		)
	);
}

void fragment() {
	vec2 scaled_uv = UV * UV_SCALE;
	
	if (is_side2(UV)){
		ALBEDO     = texture(albedo_map_2   , scaled_uv).rgb;
		ROUGHNESS  = texture(roughness_map_2, scaled_uv).r;
		METALLIC   = texture(metallic_map_2 , scaled_uv).r;
		NORMAL_MAP = texture(normal_map_2   , scaled_uv).rgb;
		AO         = texture(ao_map_2       , scaled_uv).r;
	}
	else{
		ALBEDO     = texture(albedo_map_1   , scaled_uv).rgb;
		ROUGHNESS  = texture(roughness_map_1, scaled_uv).r;
		METALLIC   = texture(metallic_map_1 , scaled_uv).r;
		NORMAL_MAP = texture(normal_map_1   , scaled_uv).rgb;
		AO         = texture(ao_map_1       , scaled_uv).r;
	}
}
