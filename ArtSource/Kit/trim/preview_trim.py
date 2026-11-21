import os
import sys

import bpy

out = sys.argv[sys.argv.index('--') + 1]
EXP = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'build', 'export')
stem = 'TrimBoard_TrimBoard_Low_ID_Pearl_'
mat = bpy.data.materials.get('Trim_Aero')
if mat:
    nt = mat.node_tree
    bsdf = nt.nodes['Principled BSDF']
    uv = nt.nodes.new('ShaderNodeUVMap')

    def img(name, colour):
        n = nt.nodes.new('ShaderNodeTexImage')
        n.image = bpy.data.images.load(os.path.join(EXP, stem + name + '.png'))
        n.image.colorspace_settings.name = 'sRGB' if colour else 'Non-Color'
        nt.links.new(uv.outputs['UV'], n.inputs['Vector'])
        return n
    alb = img('AlbedoTransparency', True)
    nt.links.new(alb.outputs['Color'], bsdf.inputs['Base Color'])
    ms = img('MetallicSmoothness', False)
    sep = nt.nodes.new('ShaderNodeSeparateColor')
    nt.links.new(ms.outputs['Color'], sep.inputs['Color'])
    nt.links.new(sep.outputs['Red'], bsdf.inputs['Metallic'])
    inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
    nt.links.new(ms.outputs['Alpha'], inv.inputs[1])
    nt.links.new(inv.outputs['Value'], bsdf.inputs['Roughness'])
    nm = img('Normal', False)
    nmap = nt.nodes.new('ShaderNodeNormalMap')
    nt.links.new(nm.outputs['Color'], nmap.inputs['Color'])
    nt.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    em = img('Emission', True)
    nt.links.new(em.outputs['Color'], bsdf.inputs['Emission Color'])
    bsdf.inputs['Emission Strength'].default_value = 2.0
bpy.ops.wm.save_as_mainfile(filepath=out)
print('PREVIEW_READY')
