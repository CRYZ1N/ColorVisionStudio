// GPU port of Core/Imaging effects (same maths). Compile with: fxc /T ps_5_0 /E main Effects.hlsl
// Not yet compiled or run: no Windows/DirectX toolchain was available. Blur runs as separate separable passes.
Texture2D src : register(t0); SamplerState smp : register(s0);
cbuffer P : register(b0) { float3x3 cvd; float cvdStrength; float vibrance; float dim; float2 pad; }
float3 toLin(float3 c){ return lerp(c/12.92, pow((c+0.055)/1.055, 2.4), step(0.04045, c)); }
float3 toSrgb(float3 c){ c=saturate(c); return lerp(c*12.92, 1.055*pow(c,1/2.4)-0.055, step(0.0031308, c)); }
float4 main(float4 pos:SV_Position, float2 uv:TEXCOORD0):SV_Target {
  float3 c = src.Sample(smp, uv).rgb;
  float3 l = toLin(c); c = toSrgb(lerp(l, mul(cvd, l), cvdStrength));
  float y = dot(c, float3(0.2126,0.7152,0.0722));
  float mx = max(c.r,max(c.g,c.b)), mn = min(c.r,min(c.g,c.b));
  float sat = mx<1e-6 ? 0 : (mx-mn)/mx;
  float gain = vibrance>1 ? 1+(vibrance-1)*(1-sat) : vibrance;
  c = saturate(y + (c-y)*gain);
  return float4(c*(1-dim), 1);
}
