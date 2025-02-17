// Based on https://casual-effects.com/research/McGuire2011Clipping/

const float clipEpsilon  = 0.00001;
const float clipEpsilon2 = 0.01;

struct VertexData
{
    float3 pos, normal;
    float2 uv;  
};

VertexData lerp(VertexData a, VertexData b, float c)
{
    a.pos = lerp(a.pos, b.pos, c);
    a.normal = lerp(a.normal, b.normal, c);
    a.uv = lerp(a.uv, b.uv, c);
    return a;
}

int clip3(const in float3 n, inout VertexData v0, inout VertexData v1, inout VertexData v2, inout VertexData v3) {

    // Distances to the plane (this is an array parallel to v[], stored as a float3)
    float3 dist = float3(dot(v0.pos, n), dot(v1.pos, n), dot(v2.pos, n));

    if (! (dist.x >= clipEpsilon2 || dist.y >= clipEpsilon2 || dist.z >= clipEpsilon2) ) {
        // All clipped
        return 0;
    }  
    
    if (dist.x >= -clipEpsilon && dist.y >= -clipEpsilon && dist.z >= -clipEpsilon) {
        // None clipped (original triangle vertices are unmodified)
        v3 = v0;
        return 3;

    }
        
    bool above[3];
    above[0] = dist.x >= 0;
    above[1] = dist.y >= 0;
    above[2] = dist.z >= 0;

    // There are either 1 or 2 vertices above the clipping plane.
    bool nextIsAbove;

    // Find the CCW-most vertex above the plane by cycling
    // the vertices in place.  There are three cases.
    if (above[1] && ! above[0]) {
        nextIsAbove = above[2];
        // Cycle once CCW.  Use v3 as a temp
        v3 = v0; v0 = v1; v1 = v2; v2 = v3;
        dist = dist.yzx;
    } else if (above[2] && ! above[1]) {
        // Cycle once CW.  Use v3 as a temp.
        nextIsAbove = above[0];
        v3 = v2; v2 = v1; v1 = v0; v0 = v3;
        dist = dist.zxy;
    } else {
        nextIsAbove = above[1];
    }
    // Note: The above[] values are no longer in sync with v values and dist[].

    // We always need to clip v2-v0.
    v3 = lerp(v0, v2, dist[0] / (dist[0] - dist[2]));

    if (nextIsAbove) {

        // There is a quadrilateral above the plane
        //
        //    v0---------v1
        //      \        |
        //   ....v3......v2'...
        //          \    |
        //            \  |
        //              v2

        v2 = lerp(v1, v2, dist[1] / (dist[1] - dist[2]));
        return 4;
    } else {

        // There is a triangle above the plane
        //
        //            v0
        //           / |
        //         /   |
        //   ....v2'..v1'...
        //      /      |
        //    v2-------v1

        v1 = lerp(v0, v1, dist[0] / (dist[0] - dist[1]));
        v2 = v3;
        v3 = v0;
        return 3;
    }
}


int clip4(const in float3 n, inout VertexData v0, inout VertexData v1, inout VertexData v2, inout VertexData v3, inout VertexData v4) {
    // Distances to the plane (this is an array parallel to v[], stored as a float4)
    float4 dist = float4(dot(v0.pos, n), dot(v1.pos, n), dot(v2.pos, n), dot(v3.pos, n));

    const float epsilon = 0.00001;

    if (! (dist.x >= clipEpsilon2 || dist.y >= clipEpsilon2 || dist.z >= clipEpsilon2 || dist.w >= clipEpsilon2) ) {
        // All clipped;
        return 0;
    } 
    
    if (dist.x >= -clipEpsilon && dist.y >= -clipEpsilon && dist.z >= -clipEpsilon && dist.w >= -clipEpsilon) {
        // None clipped (original quad vertices are unmodified)
        v4 = v0;
        return 4;
    }
    
    // There are exactly 1, 2, or 3 vertices above the clipping plane.

    bool above[4];
    above[0] = dist.x >= 0;
    above[1] = dist.y >= 0;
    above[2] = dist.z >= 0;
    above[3] = dist.w >= 0;

    // Make v0 the ccw-most vertex above the plane by cycling
    // the vertices in place.  There are four cases.
    if (above[1] && ! above[0]) {
        // v1 is the CCW-most, so cycle values CCW
        // using v4 as a temp.
        v4 = v0; v0 = v1; v1 = v2; v2 = v3; v3 = v4;
        dist = dist.yzwx;
    } else if (above[2] && ! above[1]) {
        // v2 is the CCW-most. Cycle twice CW using v4 as a temp, i.e., swap v0 with v2 and v3 with v1.
        v4 = v0; v0 = v2; v2 = v4;
        v4 = v1; v1 = v3; v3 = v4;
        dist = dist.zwxy;
    } else if (above[3] && ! above[2]) {
        // v3 is the CCW-most, so cycle values CW using v4 as a temp
        v4 = v0; v0 = v3; v3 = v2; v2 = v1; v1 = v4;
        dist = dist.wxyz;
    }

    // Note: The above[] values are no longer in sync with v values and and dist[].

    // We now need to clip along edge v3-v0 and one of edge v0-v1, v1-v2, or v2-v3.
    // Since we *always* have to clip v3-v0, compute that first and store the result in v4.
    v4 = lerp(v0, v3, dist[0] / (dist[0] - dist[3]));

    int numAbove = int(above[0]) + int(above[1]) + int(above[2]) + int(above[3]);
    if (numAbove == 1)
    {
        // Clip v0-v1, output a triangle
        //
        //            v0
        //           / |
        //         /   |
        //   ...v3'....v1'...
        //      /      |
        //    v3--v2---v1

        v1 = lerp(v0, v1, dist[0] / (dist[0] - dist[1]));
        v2 = v4;
        v3 = v4 = v0;
        return 3;

    } else if (numAbove == 2) {
        // Clip v1-v2, output a quadrilateral
        //
        //    v0-----------v1
        //      \           |
        //   ....v3'...... v2'...
        //          \       |
        //            v3---v2
        //              

        v2 = lerp(v1, v2, dist[1] / (dist[1] - dist[2]));
        v3 = v4;
        v4 = v0;
        return 4;

    } else {
        // Clip v2-v3, output a pentagon
        //
        //    v0----v1----v2
        //      \        |
        //   .....v4....v3'...
        //          \   |
        //            v3
        //              
        v3 = lerp(v2, v3, dist[2] / (dist[2] - dist[3]));
        return 5;
    } // switch
} 
