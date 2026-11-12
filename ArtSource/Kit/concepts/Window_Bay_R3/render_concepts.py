"""Disposable R3 window-bay studies. Blender 5.3; no kit imports or exports.
Scene axes: X=Unity X, Y=Unity Z (street positive), Z=Unity Y.
All files are written beside this source. See README.md for reproduction.
"""
import bpy, math, os, sys, json, argparse, warnings
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
warnings.filterwarnings('ignore', category=DeprecationWarning)
ROOT = os.path.dirname(os.path.abspath(__file__))
os.environ['OPTIX_CACHE_PATH'] = os.path.join(ROOT, 'cache')
os.makedirs(os.environ['OPTIX_CACHE_PATH'], exist_ok=True)
DIRS = ['01_White_Trays', '02_Blue_Spandrel', '03_Split_Blades']
META = {}
args = argparse.ArgumentParser()
args.add_argument('--direction', type=int, default=-1)
args.add_argument('--shots', default='all')
args.add_argument('--preview', action='store_true')
args.add_argument('--force', action='store_true')
OPT = args.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])

def rgb(h):
    c=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    return tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in c)

def mat(name,h,metal=0,rough=.5,spec=.22,emission=0):
    m=bpy.data.materials.new(name); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*rgb(h),1)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
    p.inputs['Specular IOR Level'].default_value=spec
    if emission:
        p.inputs['Emission Color'].default_value=(*rgb(h),1)
        p.inputs['Emission Strength'].default_value=emission
    return m

def materials():
    global WHITE,SILVER,NAVY,COBALT,CARPET,SEAM,TILE,LIGHT,GLASS,SUIT,SHIRT,CUT
    WHITE=mat('Wall_PaperWhite','F3F4F4',rough=.42)
    SILVER=mat('Frame_Aluminium','C9CED3',metal=.65,rough=.26)
    NAVY=mat('Trim_Navy_PROPOSAL','111B38',rough=.32)
    COBALT=mat('Glass_Spandrel_PROPOSAL_backpainted_cobalt','244FB9',rough=.16,spec=.55)
    p=COBALT.node_tree.nodes.get('Principled BSDF'); p.inputs['Coat Weight'].default_value=.45
    p.inputs['Coat Roughness'].default_value=.18
    CARPET=mat('Floor_Carpet_context','8A94A3',rough=.98,spec=.05)
    # A fine procedural weave is staging only, not a new proposed kit material.
    n=CARPET.node_tree.nodes; l=CARPET.node_tree.links
    tex=n.new('ShaderNodeTexNoise'); tex.inputs['Scale'].default_value=650
    bump=n.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=.18; bump.inputs['Distance'].default_value=.002
    l.new(tex.outputs['Fac'],bump.inputs['Height']); l.new(bump.outputs['Normal'],n.get('Principled BSDF').inputs['Normal'])
    SEAM=mat('Context tile joints','67778E',rough=.95)
    TILE=mat('Ceiling_Tile_context','F3F4F4',rough=.9)
    LIGHT=mat('Light_Strip_PROPOSAL_context_only','F3F4F4',rough=.5,emission=4)
    SUIT=mat('Scale figure Deep Navy #111B38','111B38',rough=1,spec=0)
    SHIRT=mat('Scale figure shirt','F3F4F4',rough=.9,spec=0)
    CUT=mat('Section cut face','34516E',rough=.8)
    GLASS=bpy.data.materials.new('Glass_Clear_concept_5pct_whole_pane'); GLASS.use_nodes=True
    n=GLASS.node_tree.nodes; n.clear(); l=GLASS.node_tree.links
    out=n.new('ShaderNodeOutputMaterial'); mix=n.new('ShaderNodeMixShader')
    trans=n.new('ShaderNodeBsdfTransparent'); trans.inputs[0].default_value=(1,1,1,1)
    surf=n.new('ShaderNodeBsdfDiffuse'); surf.inputs[0].default_value=(*rgb('19344C'),1)
    # Two faces of a closed 20 mm sheet together give 95% direct transparency.
    # A dark, nonreflective surface avoids the pale diffuse film in round 2.
    mix.inputs[0].default_value=1-math.sqrt(.95)
    l.new(trans.outputs[0],mix.inputs[1]); l.new(surf.outputs[0],mix.inputs[2]); l.new(mix.outputs[0],out.inputs[0])

def mesh(name,verts,faces,material):
    m=bpy.data.meshes.new(name); m.from_pydata(verts,[],faces); m.update()
    o=bpy.data.objects.new(name,m); bpy.context.collection.objects.link(o); o.data.materials.append(material)
    return o

def box(name,x0,x1,y0,y1,z0,z1,material,bevel=0):
    verts=[(x,y,z) for z in (z0,z1) for y in (y0,y1) for x in (x0,x1)]
    o=mesh(name,verts,[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],material)
    if bevel:
        b=o.modifiers.new('Inward manufactured edge','BEVEL'); b.width=bevel; b.segments=3
        o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return o

def face_finish(o,material,axis=1,positive=True):
    o.data.materials.append(material)
    for p in o.data.polygons:
        if p.normal[axis]*(1 if positive else -1)>.9:p.material_index=len(o.data.materials)-1

def xy_prism(name,poly,z0,z1,material):
    n=len(poly); v=[(x,y,z) for z in (z0,z1) for x,y in poly]
    return mesh(name,v,[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],material)

def yz_prism(name,poly,x0,x1,material):
    n=len(poly); v=[(x,y,z) for x in (x0,x1) for y,z in poly]
    return mesh(name,v,[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],material)

def column(x,z,d,corner=None):
    a,b=(x-.2,x+.2) if not corner else ((x-.02,x+.4) if corner=='L' else (x-.4,x+.02))
    if d==0:
        # A 160 mm setback: piers retreat into the dark opening above each tray.
        box('Tray pier lower full envelope',a,b,-.22,.02,z,z+1.05,WHITE)
        core=box('Tray pier recessed 160mm',a+.02,b-.02,-.22,-.14,z+1.05,z+4.16,WHITE)
        face_finish(core,NAVY)
        for lo,hi in [(a,a+.02),(b-.02,b)]:
            land=box('Tray pier full depth side snap land',lo,hi,-.22,.02,z+1.05,z+4.16,WHITE)
            face_finish(land,NAVY)
        box('Tray pier top snap land',a,b,-.22,.02,z+4.16,z+4.3,WHITE)
    elif d==1:
        core=box('White pier above blue band',a,b,-.22,.02,z+1,z+4.3,WHITE)
        base=box('Spandrel band crosses pier base',a,b,-.22,.02,z,z+1,WHITE)
        face_finish(base,COBALT)
    else:
        mid=(a+b)/2; slot=.14; f=(b-a-slot)/2
        box('Blade full bottom snap cap',a,b,-.22,.02,z,z+.055,WHITE)
        box('Blade full top snap cap',a,b,-.22,.02,z+4.245,z+4.3,WHITE)
        core=box('Blade continuous rear web',a,b,-.22,-.16,z+.055,z+4.245,WHITE)
        face_finish(core,NAVY)
        # 180 mm deep open slot; 100 mm chamfers reduce each street tip to 55 mm.
        xy_prism('Left sharp white blade',[(a,-.16),(a+f,-.16),(a+.055,.02),(a,.02)],z+.055,z+4.245,WHITE)
        xy_prism('Right sharp white blade',[(b-f,-.16),(b,-.16),(b,.02),(b-.055,.02)],z+.055,z+4.245,WHITE)

def bay(x,w,z,d,rail='B'):
    r=SILVER if rail=='A' else NAVY
    if d==0:
        # Backing is a continuous opaque 80 mm cover wall at every height.
        box('Tray solid cover backing',x,x+w,-.2,-.12,z,z+1,WHITE)
        box('Tray broad white street face',x,x+w,-.12,0,z,z+.84,WHITE)
        bed=box('Tray 120mm deep shadow bed',x+.04,x+w-.04,-.1201,-.1199,z+.84,z+1,NAVY)
        for a,b in [(x,x+.04),(x+w-.04,x+w)]:box('Tray unchanged end snap land',a,b,-.12,0,z+.84,z+1,WHITE)
    elif d==1:
        o=box('Solid sill flush backpainted glass street finish',x,x+w,-.2,0,z,z+1,WHITE)
        face_finish(o,COBALT)
    else:
        for a,b in [(x,x+.04),(x+w-.04,x+w)]:box('Blade sill unchanged end snap land',a,b,-.2,0,z,z+1,WHITE)
        yz_prism('Blade sill 200mm high folded chamfer',[(-.2,z),(0,z),(0,z+.8),(-.12,z+1),(-.2,z+1)],x+.04,x+w-.04,WHITE)
    for za,zb in [(z+1,z+1.05),(z+4.25,z+4.3)]:box('Rail finish '+rail,x+.2,x+w-.2,-.15,-.05,za,zb,r)
    box('Uninterrupted 20mm clear pane',x+.2,x+w-.2,-.11,-.09,z+1.05,z+4.25,GLASS)

def rod(name,a,b,r,material):
    c=bpy.data.curves.new(name,'CURVE'); c.dimensions='3D'; c.bevel_depth=r; c.bevel_resolution=1
    s=c.splines.new('POLY'); s.points.add(1)
    for p,v in zip(s.points,[a,b]):p.co=(*v,1)
    o=bpy.data.objects.new(name,c); bpy.context.collection.objects.link(o); o.data.materials.append(material)

def pivot(x=0,y=-.24,z=0):
    rod('Pivot at original snap corner',(x-.09,y,z),(x+.09,y,z),.009,COBALT)
    rod('Pivot at floor y=0',(x,y,z-.09),(x,y,z+.09),.009,COBALT)

def figure(x,y,z=0):
    for dx in [-.1,.1]:
        box('Dark navy trousers',x+dx-.065,x+dx+.065,y-.07,y+.07,z+.07,z+.88,SUIT,.025)
        box('Dark shoes',x+dx-.073,x+dx+.073,y-.10,y+.14,z,z+.1,SUIT,.02)
    yz_prism('Tailored navy jacket',[(-.12,z+.86),(.12,z+.86),(.15,z+1.4),(.09,z+1.49),(-.09,z+1.49),(-.15,z+1.4)],x-.22,x+.22,SUIT)
    for dx in [-.275,.275]:box('Navy sleeve',x+dx-.055,x+dx+.055,y-.075,y+.075,z+.83,z+1.43,SUIT,.04)
    # Move jacket profile from local to figure Y; trousers and head remain world-space.
    o=bpy.context.scene.objects.get('Tailored navy jacket')
    # Names are auto-suffixed for later figures, so use the newly linked jacket by prefix + geometry height.
    jackets=[o for o in bpy.context.scene.objects if o.name.startswith('Tailored navy jacket') and not o.get('positioned')]
    for o in jackets:o.location.y=y; o['positioned']=True
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,location=(x,y,z+1.655))
    o=bpy.context.object; o.name='Navy scale head 1.80m'; o.scale=(.115,.105,.145); o.data.materials.append(SUIT)
    for p in o.data.polygons:p.use_smooth=True
    # A tiny white shirt on the street-facing side makes these suits, not grey mannequins.
    mesh('White shirt V',[(x-.055,y+.151,z+1.455),(x+.055,y+.151,z+1.455),(x,y+.151,z+1.27)],[(0,1,2)],SHIRT)

def area(name,pos,target,power,size,colour=(1,1,1)):
    bpy.ops.object.light_add(type='AREA',location=pos); o=bpy.context.object; o.name=name
    o.data.energy=power; o.data.shape='DISK'; o.data.size=size; o.data.color=colour
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()

def camera(pos,target,ortho=None,lens=24):
    bpy.ops.object.camera_add(location=pos); c=bpy.context.object
    c.rotation_euler=(Vector(target)-c.location).to_track_quat('-Z','Y').to_euler()
    c.data.lens=lens; c.data.clip_end=600
    if ortho:c.data.type='ORTHO'; c.data.ortho_scale=ortho
    bpy.context.scene.camera=c

def setup(studio=False):
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for coll in (bpy.data.materials,bpy.data.meshes,bpy.data.curves,bpy.data.cameras,bpy.data.lights):
        for item in list(coll):
            if item.users==0:coll.remove(item)
    s=bpy.context.scene; s.render.engine='CYCLES'; s.cycles.samples=20 if OPT.preview else 64
    s.cycles.use_denoising=True; s.cycles.max_bounces=8; s.cycles.transparent_max_bounces=16
    p=bpy.context.preferences.addons['cycles'].preferences; p.compute_device_type='OPTIX'; p.get_devices()
    for dev in p.devices:dev.use=(dev.type=='OPTIX')
    s.cycles.device='GPU'; s.render.resolution_percentage=60 if OPT.preview else 100
    s.render.resolution_x=1600; s.render.resolution_y=1200
    s.render.image_settings.file_format='PNG'; s.render.film_transparent=False
    s.view_settings.view_transform='AgX'; s.view_settings.look='AgX - High Contrast'; s.view_settings.exposure=.1
    w=s.world; w.use_nodes=True; n=w.node_tree.nodes; n.clear(); l=w.node_tree.links
    out=n.new('ShaderNodeOutputWorld'); bg=n.new('ShaderNodeBackground'); l.new(bg.outputs[0],out.inputs[0])
    if studio:
        bg.inputs[0].default_value=(*rgb('B8C9DD'),1); bg.inputs[1].default_value=.6
        cam_bg=n.new('ShaderNodeBackground'); cam_bg.inputs[0].default_value=(*rgb('58708C'),1); cam_bg.inputs[1].default_value=.7
        lp=n.new('ShaderNodeLightPath'); mix=n.new('ShaderNodeMixShader')
        l.new(lp.outputs['Is Camera Ray'],mix.inputs[0]); l.new(bg.outputs[0],mix.inputs[1]); l.new(cam_bg.outputs[0],mix.inputs[2]); l.new(mix.outputs[0],out.inputs[0])
    else:
        coord=n.new('ShaderNodeTexCoord'); sep=n.new('ShaderNodeSeparateXYZ'); ramp=n.new('ShaderNodeValToRGB')
        l.new(coord.outputs['Normal'],sep.inputs[0])
        invert=n.new('ShaderNodeMath'); invert.operation='MULTIPLY'; invert.inputs[1].default_value=-1
        l.new(sep.outputs['Z'],invert.inputs[0]); l.new(invert.outputs[0],ramp.inputs[0])
        ramp.color_ramp.elements[0].position=0; ramp.color_ramp.elements[0].color=(*rgb('A4C7EB'),1)
        ramp.color_ramp.elements[1].position=.4; ramp.color_ramp.elements[1].color=(*rgb('0A34AC'),1)
        mid=ramp.color_ramp.elements.new(.06); mid.color=(*rgb('2865DF'),1)
        l.new(ramp.outputs[0],bg.inputs[0]); bg.inputs[1].default_value=.32
        # Camera sees the saturated sky, independently of low ambient irradiance.
        cam_bg=n.new('ShaderNodeBackground'); cam_bg.inputs[1].default_value=1
        l.new(ramp.outputs[0],cam_bg.inputs[0])
        lp=n.new('ShaderNodeLightPath'); mix=n.new('ShaderNodeMixShader')
        l.new(lp.outputs['Is Camera Ray'],mix.inputs[0]); l.new(bg.outputs[0],mix.inputs[1]); l.new(cam_bg.outputs[0],mix.inputs[2]); l.new(mix.outputs[0],out.inputs[0])
    materials()
    bpy.ops.object.light_add(type='SUN',location=(-24,12,18)); sun=bpy.context.object; sun.name='Hard street sun / raking'
    sun.data.energy=4; sun.data.angle=math.radians(.65); sun.data.color=(1,.97,.93)
    sun.rotation_euler=(Vector((6,-4,0))-sun.location).to_track_quat('-Z','Y').to_euler()
    if studio:area('Studio softbox',(2,-6,7),(2,0,2),500,5)

def city():
    pale=mat('Context city porcelain','D7E5F4',rough=.8)
    blue=mat('Context city cobalt','5486C8',rough=.7)
    haze=mat('Context city pale haze','BACFE7',rough=.9)
    for sign in [-1,1]:
        for i in range(13):
            x=-48+i*8.5; y=sign*(30+(i%3)*13); h=[-2,6,1,12,-4,4,9][i%7]
            box('Large undetailed city mass',x,x+4.5+(i%3),min(y,y+sign*6),max(y,y+sign*6),-30,h,blue if i%3==0 else pale)
        for i in range(9):
            x=-66+i*17; y=sign*90
            box('Hazy distant block',x,x+9,min(y,y+sign*9),max(y,y+sign*9),-30,-2+(i%5)*2,haze)
    box('Street far below',-120,120,-120,120,-31,-30.8,pale)

def room(d,rail='B',levels=(0,)):
    for z in levels:
        box('Unmodified 200mm context slab',0,12,-8,0,z-.2,z,WHITE)
        box('Grey blue carpet',0,12,-8,-.2,z,z+.006,CARPET)
        for x in range(1,12):box('Carpet 1m tile joint',x-.003,x+.003,-8,-.22,z+.006,z+.007,SEAM)
        for y in range(-7,0):box('Carpet 1m tile joint',0,12,y-.003,y+.003,z+.006,z+.007,SEAM)
        box('Ceiling 200mm context slab',0,12,-8,0,z+4.3,z+4.5,WHITE)
        for x in range(13):box('Ceiling tile grid',x-.008,x+.008,-8,0,z+4.29,z+4.3,SILVER)
        for y in range(-8,1):box('Ceiling tile grid',0,12,y-.008,y+.008,z+4.29,z+4.3,SILVER)
        for x in [2,6,10]:
            for y in [-2,-5]:box('Office ceiling strip placeholder',x-.45,x+.45,y-.035,y+.035,z+4.27,z+4.29,LIGHT)
        box('Bright Paper White interior background',0,12,-8.2,-8,z,z+4.3,WHITE)
        for a,b in [(0,.2),(11.8,12)]:box('Room return wall',a,b,-8,-.2,z,z+4.3,WHITE)
        for x in [0,4,8]:bay(x,4,z,d,rail)
        for x in [0,4,8,12]:column(x,z,d,'L' if x==0 else ('R' if x==12 else None))
        # Light points into the wall from behind the suits; their dark front remains dark.
        area('Bright back wall wash',(6,-5.8,z+3.2),(6,-8,z+2),650,8)

def shot(d,name,dims=None):
    folder=os.path.join(ROOT,DIRS[d]); os.makedirs(folder,exist_ok=True)
    path=os.path.join(folder,name+'.png'); s=bpy.context.scene
    bpy.context.view_layer.update(); s.render.filepath=path
    if OPT.force or not os.path.exists(path):bpy.ops.render.render(write_still=True)
    w=int(s.render.resolution_x*s.render.resolution_percentage/100); h=int(s.render.resolution_y*s.render.resolution_percentage/100)
    annotations=[]
    for a,b,label,offset in dims or []:
        pts=[]
        for v in (a,b):
            p=world_to_camera_view(s,s.camera,Vector(v)); pts.append([p.x*w,(1-p.y)*h])
        annotations.append(dict(a=pts[0],b=pts[1],label=label,offset=offset))
    META[os.path.relpath(path,ROOT)]=dict(dimensions=annotations,camera=list(s.camera.location),lens=s.camera.data.lens,resolution=[w,h])
    print('R3_FINISHED',DIRS[d],name,flush=True)
    with open(os.path.join(ROOT,'render_metadata.json'),'w',encoding='utf-8') as f:json.dump(META,f,indent=2)

def wanted(name):return OPT.shots=='all' or name in OPT.shots.split(',')

def execute():
    path=os.path.join(ROOT,'render_metadata.json')
    if os.path.exists(path):
        with open(path,encoding='utf-8') as f:META.update(json.load(f))
    for d in range(3) if OPT.direction<0 else [OPT.direction]:
        for rail in ['B','A']:
            if wanted('rival_'+rail):
                setup(); room(d,rail,(-4.5,0,4.5)); city()
                figure(2.2,-.5); figure(6.3,-.7); figure(10,-.3,4.5)
                box('Observer second pane',-30,40,11.69,11.71,-15,25,GLASS)
                camera((6,12,1.7),(6,0,1.7),lens=20)
                shot(d,'rival_'+rail)
            if wanted('room_'+rail):
                setup(); room(d,rail); city(); figure(9,-.7)
                camera((2.6,-7,1.7),(6.1,0,2.05),lens=20)
                shot(d,'room_'+rail)
        if wanted('close'):
            setup(); room(d); city(); camera((5.6,2.05,1.75),(4,-.04,1.3),lens=38)
            shot(d,'close')
        if wanted('front'):
            setup(True); bay(0,4,0,d); column(0,0,d); column(4,0,d); figure(5,-.1); pivot()
            camera((2.5,-14,2.1),(2.5,0,2.1),ortho=8)
            shot(d,'front',[
                ((0,-.25,0),(4,-.25,0),'4.00 m grid run',54),
                ((-.2,-.25,0),(-.2,-.25,4.3),'4.30 m clear height',-65),
                ((4.35,-.25,0),(4.35,-.25,1),'1.00 m solid cover',40),
                ((.2,-.25,4.25),(3.8,-.25,4.25),'3.60 m clear pane',-45)])
        if wanted('side'):
            setup(True); bay(0,4,0,d); column(4,0,d); figure(4,-1); pivot(4)
            camera((16,-.1,2.1),(4,-.1,2.1),ortho=7)
            shot(d,'side',[
                ((4,-.22,.2),(4,.02,.2),'0.24 m column depth',55),
                ((4,.18,0),(4,.18,4.3),'4.30 m',70)])
        if wanted('three_quarter'):
            setup(True); bay(0,4,0,d); column(0,0,d,'L'); column(4,0,d); figure(3.2,-.7); pivot()
            box('Lower context slab',0,4,-1.4,0,-.2,0,WHITE); box('Upper context slab',0,4,-1.4,0,4.3,4.5,WHITE)
            camera((8,7,5.6),(2,-.1,2),ortho=7.9); shot(d,'three_quarter')
        if wanted('room_three_quarter'):
            setup(True); bay(0,4,0,d); column(0,0,d,'L'); column(4,0,d); figure(3.2,-.7); pivot()
            box('Lower context slab',0,4,-1.4,0,-.2,0,WHITE); box('Upper context slab',0,4,-1.4,0,4.3,4.5,WHITE)
            camera((8,-7,5.6),(2,-.1,2),ortho=7.9); shot(d,'room_three_quarter')
        if wanted('section'):
            setup(True); bay(0,4,0,d)
            # Actual middle-of-run section; retain the original Y/Z profile.
            for o in list(bpy.context.scene.objects):
                if o.type=='MESH':
                    xs=[v.co.x for v in o.data.vertices]
                    if max(xs)<1.98 or min(xs)>2.02:
                        bpy.data.objects.remove(o,do_unlink=True)
                        continue
                    for v in o.data.vertices:v.co.x=2.02 if v.co.x>2 else 1.98
                    o.data.update()
                    if not any(m==GLASS for m in o.data.materials):face_finish(o,CUT,axis=0)
            box('Section lower slab',1.98,2.02,-1,0,-.2,0,CUT)
            box('Section upper slab',1.98,2.02,-1,0,4.3,4.5,CUT)
            figure(2,-1.6); pivot(2)
            camera((14,-.1,2.1),(2,-.1,2.1),ortho=7.4)
            shot(d,'section',[
                ((2,-.2,.15),(2,0,.15),'0.20 m sill envelope',75),
                ((2,.25,1.05),(2,.25,4.25),'3.20 m clear pane',65),
                ((2,-1,-.2),(2,-1,0),'0.20 m slab',-90),
                ((2,-.11,3.6),(2,-.09,3.6),'0.02 m pane',-100)])
        if wanted('infill_corners'):
            setup(True); bay(0,2,0,d); column(0,0,d,'L'); column(2,0,d,'R'); figure(3.2,-.6); pivot()
            box('L return context',0,.2,-1,-.2,0,4.3,WHITE); box('R return context',1.8,2,-1,-.2,0,4.3,WHITE)
            camera((5,6,4.5),(1.2,-.1,2),ortho=6.9); shot(d,'infill_corners')

if __name__=='__main__':execute()
