"""Window bay R2: disposable, dimensioned concept geometry, not production assets.
Run Blender -b --factory-startup -P <this file>. All outputs stay beside this script.
Coordinates for these render scenes: X = kit X, Y = kit Z, Z = kit Y.
"""
import bpy, math, os, json, warnings
warnings.filterwarnings('ignore', category=DeprecationWarning)
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

ROOT = os.path.dirname(os.path.abspath(__file__))
DIRECTIONS = [('01_Dark_Bands', 'DARK BANDS'), ('02_Lit_Piers', 'LIT PIERS'), ('03_Cobalt_Corners', 'COBALT CORNERS')]
META = {}

def material(name, colour, metal=0, rough=.55, emission=0):
    m=bpy.data.materials.new(name); m.use_nodes=True
    rgb=tuple(int(colour[i:i+2],16)/255 for i in (0,2,4))
    rgb=tuple(v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb)
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*rgb,1)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
    p.inputs['Specular IOR Level'].default_value=.18
    if emission:
        p.inputs['Emission Color'].default_value=(*rgb,1); p.inputs['Emission Strength'].default_value=emission
    return m

def materials():
    global white, silver, navy, cobalt, carpet, glass, light, ink
    white=material('Wall_PaperWhite','F3F4F4'); silver=material('Frame_Aluminium','C9CED3',.45,.3)
    navy=material('Trim_Navy_PROPOSAL','111B38'); cobalt=material('Accent_Cobalt_PROPOSAL','244FB9')
    carpet=material('Floor_Carpet','8A94A3',0,.95); light=material('Light_Strip_PROPOSAL','F3F4F4',0,.25,3)
    ink=material('Section_cut','34465B')
    glass=bpy.data.materials.new('Glass_Clear_concept_95pct_transparent'); glass.use_nodes=True
    n=glass.node_tree.nodes; n.clear(); out=n.new('ShaderNodeOutputMaterial'); mix=n.new('ShaderNodeMixShader')
    trans=n.new('ShaderNodeBsdfTransparent'); surf=n.new('ShaderNodeBsdfDiffuse')
    surf.inputs['Color'].default_value=(.64,.80,.93,1); mix.inputs[0].default_value=.05
    glass.node_tree.links.new(trans.outputs[0],mix.inputs[1]); glass.node_tree.links.new(surf.outputs[0],mix.inputs[2]); glass.node_tree.links.new(mix.outputs[0],out.inputs[0])
    glass.surface_render_method='DITHERED'

def box(name, x0,x1,y0,y1,z0,z1, mat, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=((x0+x1)/2,(y0+y1)/2,(z0+z1)/2))
    o=bpy.context.object; o.name=name; o.dimensions=(x1-x0,y1-y0,z1-z0)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mat)
    if bevel:
        b=o.modifiers.new('Inward edge radius','BEVEL'); b.width=bevel; b.segments=4
        o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return o

def prism(name, coords, y0,y1, mat):
    verts=[(x,y,z) for y in (y0,y1) for x,z in coords]; n=len(coords)
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    ob=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(ob); ob.data.materials.append(mat)
    return ob

def recess(ob, x0,x1,y0,y1,z0,z1):
    cutter=box('Temporary reveal cutter',x0,x1,y0,y1,z0,z1,white)
    bpy.context.view_layer.objects.active=ob
    mod=ob.modifiers.new('Carved reveal','BOOLEAN'); mod.operation='DIFFERENCE'; mod.object=cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter,do_unlink=True)

def column(x, floor, direction, corner=None):
    lo,hi=(x-.2,x+.2) if corner is None else ((x-.02,x+.4) if corner=='L' else (x-.4,x+.02))
    z=floor
    if direction==1:
        # Full-height recessed room slot, 60 mm wide and 65 mm deep; leave top/bottom snap caps intact.
        c=(lo+hi)/2; a=c-.03; b=c+.03
        pier=box('Rounded carved light pier',lo,hi,-.22,.02,z,z+4.3,white,.018)
        # Apply bevel before cutting the recess so its physical depth is not rounded away.
        bpy.context.view_layer.objects.active=pier
        for mod in list(pier.modifiers): bpy.ops.object.modifier_apply(modifier=mod.name)
        recess(pier,a,b,-.221,-.155,z+.12,z+4.18)
        box('Recessed slot dark bed',a,b,-.156,-.155,z+.12,z+4.18,navy)
        box('Recessed luminous ribbon',c-.009,c+.009,-.159,-.157,z+.18,z+4.12,light)
    elif direction==0:
        # Two deep vertical pockets on both visible faces, no change at snap ends/top/bottom.
        c=(lo+hi)/2
        pier=box('Twin scored white pier',lo,hi,-.22,.02,z,z+4.3,white)
        for xa,xb in [(lo+.035,lo+.075),(hi-.075,hi-.035)]:
            recess(pier,xa,xb,-.221,-.16,z+.12,z+4.18)
            recess(pier,xa,xb,-.04,.021,z+.12,z+4.18)
            box('Room reveal bed',xa,xb,-.161,-.160,z+.12,z+4.18,navy)
            box('Street reveal bed',xa,xb,-.040,-.039,z+.12,z+4.18,navy)
        for za,zb in [(z+.80,z+1),(z+4.10,z+4.3)]: box('Street continuous navy belt',lo,hi,.02,.021,za,zb,navy)
    else:
        box('Chamfered white pier',lo,hi,-.22,.02,z,z+4.3,white,.018)
        if corner:
            # Graphic flush surface, every corner only. Diagonal crossing is at 2.15 m.
            prism('Cobalt corner street graphic',[(lo,z+.5),(hi,z+1.15),(hi,z+3.45),(lo,z+2.8)],.020,.021,cobalt)
            prism('Cobalt corner room graphic',[(lo,z+.5),(hi,z+1.15),(hi,z+3.45),(lo,z+2.8)],-.221,-.220,cobalt)
        else:
            box('Street pier axial pocket',lo+.17,hi-.17,.020,.021,z+.2,z+4.1,navy)

def bay(x,width,floor,direction,rail,section=False):
    z=floor; r=silver if rail=='A' else navy
    # Solid original sill retained as cover, all details lie on its surface or in visual caps.
    box('Solid cover sill',x,x+width,-.2,0,z,z+1,white)
    if direction==0:
        box('Navy sill belt',x,x+width,0,.001,z+.80,z+1,navy)
        # Head belt occupies top of existing pane; concept intentionally avoids this: only pier/head rail.
        box('Room navy sill cap',x,x+width,-.2,0,z+.982,z+1,navy)
    if direction==1:
        # Silver cap remains inside 1 m sill; 12 mm shadow line is a finish inset, cover stays intact.
        box('Silver cover cap',x,x+width,-.2,0,z+.975,z+1,silver)
        box('Room shadow under cap',x,x+width,-.201,-.200,z+.947,z+.959,navy)
        box('Street shadow under cap',x,x+width,0,.001,z+.947,z+.959,navy)
    if direction==2:
        box('Street navy plinth',x,x+width,0,.001,z,z+.18,navy)
        box('Silver sill folded top',x,x+width,-.2,0,z+.985,z+1,silver)
    for za,zb in [(z+1,z+1.05),(z+4.25,z+4.3)]:
        box('Rail_'+rail,x+.2,x+width-.2,-.15,-.05,za,zb,r)
    box('Single uninterrupted pane',x+.2,x+width-.2,-.11,-.09,z+1.05,z+4.25,glass)

def figure(x,y,z=0):
    # 1.8 m standing suit mannequin; mesh proportions, no scenic pose or props.
    for dx in [-.105,.105]:
        box('Suit trouser',x+dx-.07,x+dx+.07,y-.075,y+.075,z+.07,z+.89,navy,.035)
        box('Shoe',x+dx-.075,x+dx+.075,y-.13,y+.10,z,z+.10,navy,.03)
    box('Suit jacket',x-.22,x+.22,y-.105,y+.105,z+.86,z+1.49,navy,.065)
    for dx in [-.275,.275]: box('Sleeve',x+dx-.055,x+dx+.055,y-.065,y+.065,z+.83,z+1.44,navy,.045)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,location=(x,y,z+1.655))
    o=bpy.context.object; o.name='Navy scale figure head'; o.scale=(.115,.105,.145); o.data.materials.append(navy)
    for p in o.data.polygons:p.use_smooth=True
    prism('Shirt triangle',[(x-.07,z+1.48),(x+.07,z+1.48),(x,z+1.27)],y-.107,y-.106,white)

def line(a,b,mat=None,r=.008):
    c=bpy.data.curves.new('Pivot cross','CURVE'); c.dimensions='3D'; c.bevel_depth=r; c.bevel_resolution=2
    s=c.splines.new('POLY'); s.points.add(1)
    for p,v in zip(s.points,[a,b]): p.co=(*v,1)
    o=bpy.data.objects.new('Pivot cross',c); bpy.context.collection.objects.link(o); o.data.materials.append(mat or navy)

def pivot(x=0,y=-.23,z=0):
    line((x-.10,y,z),(x+.10,y,z),cobalt); line((x,y,z-.10),(x,y,z+.10),cobalt)

def setup():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    s=bpy.context.scene; s.render.engine='CYCLES'; s.cycles.samples=24; s.cycles.use_denoising=True
    s.cycles.max_bounces=6; s.cycles.transparent_max_bounces=12
    s.render.resolution_x=1400; s.render.resolution_y=950; s.render.resolution_percentage=100
    s.world.color=(.55,.55,.55)
    s.world.use_nodes=True; s.world.node_tree.nodes['Background'].inputs[0].default_value=(.66,.76,.94,1)
    s.world.node_tree.nodes['Background'].inputs[1].default_value=.5
    s.view_settings.view_transform='AgX'
    s.render.image_settings.file_format='PNG'; s.render.film_transparent=False
    materials()
    area('Large cool daylight',(5,4,8),(6,-2,1.5),2100,8,(.78,.87,1))
    area('Soft interior fill',(6,-5,5),(6,0,1.8),1600,9,(1,1,1))

def area(name, pos, target, energy, size, colour):
    bpy.ops.object.light_add(type='AREA',location=pos); o=bpy.context.object; o.name=name
    o.data.energy=energy; o.data.shape='DISK'; o.data.size=size; o.data.color=colour
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()

def camera(pos,target,ortho=None,lens=35):
    bpy.ops.object.camera_add(location=pos); c=bpy.context.object
    c.rotation_euler=(Vector(target)-c.location).to_track_quat('-Z','Y').to_euler()
    c.data.lens=lens; c.data.clip_end=500
    if ortho:c.data.type='ORTHO'; c.data.ortho_scale=ortho
    bpy.context.scene.camera=c

def room(direction,rail,floors=1):
    for f in range(floors):
        z=f*4.5
        box('Context slab',0,12,-9,0,z-.2,z,white)
        box('Muted carpet',0,12,-9,-.22,z,z+.003,carpet)
        box('Context ceiling slab',0,12,-9,0,z+4.3,z+4.5,white)
        box('Bright rear wall',0,12,-9.2,-9,z,z+4.3,white)
        for x in [0,4,8]:bay(x,4,z,direction,rail)
        for x in [0,4,8,12]:column(x,z,direction,'L' if x==0 else ('R' if x==12 else None))
        # Simple return at each end demonstrates handed corner alignment.
        for xa,xb in [(0,.2),(11.8,12)]:box('Return wall',xa,xb,-5,-.2,z,z+4.3,white)

def skyline():
    skywhite=material('Context distant towers','BACFEA',0,.95)
    skyblue=material('Context distant blue towers','719BC9',0,.95)
    for i in range(17):
        x=-42+i*5.7; h=[14,22,18,31,17,25,38][i%7]
        box('Abstract distant skyline',x,x+2.5+(i%3)*.7,35+(i%3)*9,38+(i%3)*9,-15,h,skywhite if i%2 else skyblue)

def shot(folder,name, dims=None):
    bpy.context.view_layer.update()
    s=bpy.context.scene; path=os.path.join(folder,name+'.png'); s.render.filepath=path
    force=os.environ.get('R2_FORCE','')
    if not os.path.exists(path) or force=='ALL' or (force and name.startswith(force)):
        bpy.ops.render.render(write_still=True)
    data=[]
    for a,b,label,offset in dims or []:
        pts=[]
        for v in (a,b):
            p=world_to_camera_view(s,s.camera,Vector(v)); pts.append([p.x*s.render.resolution_x,(1-p.y)*s.render.resolution_y])
        data.append({'a':pts[0],'b':pts[1],'label':label,'offset':offset})
    META[path]={'dimensions':data}
    with open(os.path.join(ROOT,'render_metadata.json'),'w') as f:json.dump(META,f,indent=2)

def execute():
    for d,(slug,title) in enumerate(DIRECTIONS):
        folder=os.path.join(ROOT,slug); os.makedirs(folder,exist_ok=True)
        for rail in ['A','B']:
            setup(); room(d,rail); skyline(); figure(6,-.7); pivot()
            camera((4,-7.8,1.7),(6,0,2.0),lens=18)
            shot(folder,'hero_'+rail)
            setup(); room(d,rail,2); figure(6,-.6); figure(9,-.7,4.5)
            # Camera exactly 12 m beyond street grid. Second transparent pane is the observer's window.
            box('Observer pane - two pane read',-12,24,11.69,11.71,-3,16,glass)
            camera((6,12,3.5),(6,0,4.1),lens=30)
            shot(folder,'street_'+rail)
        setup(); bay(0,4,0,d,'A'); column(0,0,d); column(4,0,d); figure(5,-.1); pivot()
        camera((2,-12,2.1),(2,0,2.1),ortho=7.8)
        shot(folder,'front',[
            ((0,-.25,0),(4,-.25,0),'4.00 m grid run',56),
            ((-.2,-.25,0),(-.2,-.25,4.3),'4.30 m',-50),
            ((4.3,-.25,0),(4.3,-.25,1),'1.00 m solid sill',28),
            ((.2,-.25,4.25),(3.8,-.25,4.25),'3.60 m clear pane',-38)])
        setup(); bay(0,4,0,d,'A'); column(4,0,d); figure(4,.85); pivot(4)
        camera((14,0,2.05),(4,0,2.05),ortho=6.7)
        shot(folder,'side',[
            ((4,-.22,.4),(4,.02,.4),'0.24 m pier',15),
            ((4,-.3,0),(4,-.3,4.3),'4.30 m',-50)])
        setup(); bay(0,4,0,d,'B'); column(0,0,d,'L'); column(4,0,d); figure(3,-.7); pivot()
        box('Lower slab context',0,4,-1,0,-.2,0,white); box('Upper slab context',0,4,-1,0,4.3,4.5,white)
        camera((8,7,5.5),(2,-.1,2),ortho=7)
        shot(folder,'three_quarter')
        setup(); bay(0,4,0,d,'B'); column(0,0,d,'L'); column(4,0,d); figure(3,-.7); pivot()
        box('Lower slab context',0,4,-1,0,-.2,0,white); box('Upper slab context',0,4,-1,0,4.3,4.5,white)
        camera((7,-7,5.4),(2,-.1,2),ortho=7.8)
        shot(folder,'room_three_quarter')
        setup(); bay(0,4,0,d,'A')
        # Mid-run cut, discard all x outside a 40 mm slice (no pier intersects the cut).
        for o in list(bpy.context.scene.objects):
            if o.type=='MESH':
                o.location.x=2; o.dimensions.x=.04
        box('Section lower slab',1.98,2.02,-.8,0,-.2,0,ink)
        box('Section upper slab',1.98,2.02,-.8,0,4.3,4.5,ink)
        figure(2,-1.4); pivot(2,-.2)
        camera((12,-.15,2.1),(2,-.15,2.1),ortho=7.8)
        shot(folder,'section',[
            ((2,-.2,0),(2,0,0),'0.20 m sill',45),
            ((2,-.11,2.8),(2,-.09,2.8),'0.02 m pane',65),
            ((2,.23,1.05),(2,.23,4.25),'3.20 m pane',40),
            ((2,-.8,-.2),(2,-.8,0),'0.20 m slab',-55)])
        setup(); bay(0,2,0,d,'A'); column(0,0,d,'L'); column(2,0,d,'R'); figure(3,-.6); pivot()
        box('CornerL return',0,.2,-1.3,-.2,0,4.3,white)
        box('CornerR return',1.8,2,-1.3,-.2,0,4.3,white)
        camera((4,-6,3.4),(1,-.2,2),ortho=8)
        shot(folder,'infill_corners')
    with open(os.path.join(ROOT,'render_metadata.json'),'w') as f:json.dump(META,f,indent=2)

if __name__=='__main__':execute()
