shader_type spatial;

uniform sampler2D albedoMap1           : source_color;
uniform sampler2D normalMap1           : hint_normal; // Combine into normal + roughness texture?
uniform sampler2D metallicMap1         : hint_default_black;
uniform sampler2D roughnessMap1        : hint_default_white;
uniform sampler2D ambientOcclusionMap1 : hint_default_white;

uniform sampler2D albedoMap2           : source_color;
uniform sampler2D normalMap2           : hint_normal;
uniform sampler2D metallicMap2         : hint_default_black;
uniform sampler2D roughnessMap2        : hint_default_white;
uniform sampler2D ambientOcclusionMap2 : hint_default_white;

const float oneThird = 1.0 / 3.0;
const vec2 UVScale = vec2(3.0, 2.0);

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
	vec2 scaledUV = UV * UVScale;
	
	if (is_side2(UV)){
		ALBEDO     = texture(albedoMap2          , scaledUV).rgb;
		NORMAL_MAP = texture(normalMap2          , scaledUV).rgb;
		METALLIC   = texture(metallicMap2        , scaledUV).r;
		ROUGHNESS  = texture(roughnessMap2       , scaledUV).r;
		AO         = texture(ambientOcclusionMap2, scaledUV).r;
	}
	else{
		ALBEDO     = texture(albedoMap1          , scaledUV).rgb;
		NORMAL_MAP = texture(normalMap1          , scaledUV).rgb;
		METALLIC   = texture(metallicMap1        , scaledUV).r;
		ROUGHNESS  = texture(roughnessMap1       , scaledUV).r;
		AO         = texture(ambientOcclusionMap1, scaledUV).r;
	}
}
