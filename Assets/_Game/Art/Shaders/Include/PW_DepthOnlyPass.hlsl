// Depth-only pass body shared by the master shaders (vertex DepthVert, fragment DepthFrag).
#ifndef PW_DEPTH_ONLY_PASS_INCLUDED
#define PW_DEPTH_ONLY_PASS_INCLUDED

float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
{
    return TransformObjectToHClip(positionOS.xyz);
}

half DepthFrag() : SV_Target
{
    return 0;
}

#endif
