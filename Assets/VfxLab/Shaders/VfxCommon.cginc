// Shared helpers for the VFX Lab shaders (Built-in Render Pipeline).
#ifndef LASTWISH_VFX_COMMON
#define LASTWISH_VFX_COMMON

// Ordered 4x4 Bayer threshold in [0,1) for a pixel coordinate (arithmetic form, no table lookups).
float VfxBayer2(float2 a) { a = floor(a); return frac(dot(a, float2(0.5, a.y * 0.75))); }
float VfxBayer4(float2 a) { a = fmod(floor(a), 4.0); return VfxBayer2(0.5 * a) * 0.25 + VfxBayer2(a) + 0.03125; }

// Posterize a soft alpha into `steps` bands with dithered transitions (pixel-art falloff).
float VfxPixelAlpha(float a, float2 pixel, float steps, float dither)
{
    if (steps < 0.5) return a;
    float b = lerp(0.5, VfxBayer4(pixel), dither);
    return saturate(floor(a * steps + b) / steps);
}

#endif
