shader_type spatial;

uniform sampler2D albedo1 : source_color;
uniform sampler2D normalMap1 : hint_normal; // Combine into normal + roughness texture?
uniform sampler2D metallicMap1 : hint_default_black;
uniform sampler2D roughnessMap1 : hint_default_white;
uniform sampler2D ambientOcculusionMap1 : hint_default_white;

uniform sampler2D albedo2 : source_color;
uniform sampler2D normalMap2 : hint_normal;
uniform sampler2D metallicMap2 : hint_default_black;
uniform sampler2D roughnessMap2 : hint_default_white;
uniform sampler2D ambientOcculusionMap2 : hint_default_white;

const float oneThird = 1.0 / 3.0;

// Only allow textures at their specified UV areas (for Godot's BoxMesh)
bool is_side2(vec2 uv){
	return
	(
		(uv.x > 0.5 * oneThird && uv.x < 2.5 * oneThird && uv.y < 0.5) ||
		(
			(
				(uv.x > oneThird && uv.x < 1.5 * oneThird) ||
				(uv.x > 2.5 * oneThird)
			) &&
			uv.y > 0.5
		)
	);
}

void fragment() {
	if (is_side2(UV)){
		ALBEDO     = texture(albedo2, UV).rgb;
		NORMAL_MAP = texture(normalMap2, UV).rgb;
		METALLIC   = texture(metallicMap2, UV).r;
		ROUGHNESS  = texture(roughnessMap2, UV).r;
		AO         = texture(ambientOcculusionMap2, UV).r;
	}
	else{
		ALBEDO     = texture(albedo1, UV).rgb;
		NORMAL_MAP = texture(normalMap1, UV).rgb;
		METALLIC   = texture(metallicMap1, UV).r;
		ROUGHNESS  = texture(roughnessMap1, UV).r;
		AO         = texture(ambientOcculusionMap1, UV).r;
	}
}
