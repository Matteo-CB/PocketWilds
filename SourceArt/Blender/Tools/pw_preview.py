"""Review helper: shows a variant palette on an animal LOD in Blender with the same blend order as
SH_AnimalMaster (primary, secondary, belly, pattern, detail, eye). Preview colors live in a temporary
WORK_Preview attribute that clear() removes before export."""
import bpy
from mathutils import Vector


def srgb(hex_color):
    c = [int(hex_color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return Vector([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c])


def _lerp(a, b, t):
    return a + (b - a) * t


def preview(obj, palette, channel=0, strength=1.0):
    """palette: (primary, secondary, belly, detail, eye, pattern or None) as hex; channel 0 = mask A (UV x), 1 = mask B (UV y)."""
    me = obj.data
    regions = me.color_attributes["PW_Regions"]
    uv = me.uv_layers["PW_Masks"]
    mask = [0.0] * len(me.vertices)
    for loop in me.loops:
        mask[loop.vertex_index] = uv.data[loop.index].uv[channel]
    p, s, b, d, e, pat = palette
    p, s, b, d, e = srgb(p), srgb(s), srgb(b), srgb(d), srgb(e)
    pat_c = srgb(pat) if pat else p
    k = strength if pat else 0.0
    pv = me.color_attributes.get("WORK_Preview")
    if pv is None or pv.data_type != 'FLOAT_COLOR':
        if pv is not None:
            me.color_attributes.remove(pv)
        pv = me.color_attributes.new("WORK_Preview", 'FLOAT_COLOR', 'POINT')
    for i, data in enumerate(regions.data):
        r, g, bb, a = data.color
        c = p
        c = _lerp(c, s, r)
        c = _lerp(c, b, g)
        c = _lerp(c, pat_c, mask[i] * k)
        c = _lerp(c, d, bb)
        c = _lerp(c, e, a)
        pv.data[i].color = (c.x, c.y, c.z, 1.0)
    me.color_attributes.active_color = pv


def clear(obj):
    me = obj.data
    pv = me.color_attributes.get("WORK_Preview")
    if pv is not None:
        me.color_attributes.remove(pv)
    me.color_attributes.active_color = me.color_attributes["PW_Regions"]
    me.color_attributes.render_color_index = me.color_attributes.active_color_index
