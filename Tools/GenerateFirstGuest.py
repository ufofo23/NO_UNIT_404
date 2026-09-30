"""Original mesh art for the first currently scheduled guest. Run with Blender -b -P.
Blender source stays outside Assets to avoid requiring Blender on teammates' machines.
Coordinates: metres, Z up, face towards -Y (Unity +Z after FBX import).
"""
import bpy, math, os
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets/_Project/Resources/NO404/Characters/FirstGuest')
SOURCE = os.path.join(ROOT, 'Docs/Art/FirstGuest')
os.makedirs(OUT, exist_ok=True)
os.makedirs(SOURCE, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def mat(name, color, rough=.7, metal=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal
    return m

skin = mat('Skin', (.52,.39,.32))
skin_shadow = mat('SkinShadow', (.36,.19,.14))
hair = mat('Hair', (.022,.025,.028))
jacket = mat('SageCanvas', (.19,.245,.215))
seam = mat('CanvasSeams', (.115,.153,.134))
shirt = mat('CreamCotton', (.64,.61,.51))
pants = mat('CharcoalDenim', (.055,.071,.085))
rubber = mat('ShoeRubber', (.035,.037,.034))
sole = mat('WornSoles', (.41,.40,.35))
white = mat('EyeWhite', (.65,.63,.56), .35)
iris = mat('DarkBrown', (.055,.035,.022), .3)
metal = mat('BrushedMetal', (.35,.38,.37), .32, .75)
screen = mat('PhoneGlass', (.06,.14,.16), .24, .3)

def node(name, pos, parent=None):
    o=bpy.data.objects.new(name,None)
    bpy.context.collection.objects.link(o)
    o.location=pos
    if parent:
        o.parent=parent
        o.matrix_parent_inverse=parent.matrix_world.inverted()
    bpy.context.view_layer.update()
    return o

root=node('FirstGuest', (0,0,0))
body=node('BodyPivot', (0,0,1.0), root)
head=node('HeadPivot',(0,0,1.52),body)
la=node('LeftArmPivot',(.245,0,1.42),body)
ra=node('RightArmPivot',(-.245,0,1.42),body)
ll=node('LeftLegPivot',(.105,0,.90),root)
rl=node('RightLegPivot',(-.105,0,.90),root)

def finish(o,name,m,parent):
    o.name=name
    o.data.materials.append(m)
    if parent:
        o.parent=parent
        o.matrix_parent_inverse=parent.matrix_world.inverted()
    return o

def ell(name,pos,scale,m,parent=body,segments=24,rings=14):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=pos)
    o=bpy.context.object
    o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for p in o.data.polygons:p.use_smooth=True
    return finish(o,name,m,parent)

def box(name,pos,scale,m,parent=body,bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    o=bpy.context.object
    o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Soft tailored edges','BEVEL');mod.width=bevel;mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o,name,m,parent)

def segment(name,a,b,r1,r2,m,parent,vertices=16):
    a,b=Vector(a),Vector(b)
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r1,radius2=r2,depth=(b-a).length,location=(a+b)/2)
    o=bpy.context.object
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    for p in o.data.polygons:p.use_smooth=len(p.vertices)==4
    return finish(o,name,m,parent)

def tube(name,points,radius,m,parent):
    c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.bevel_depth=radius;c.bevel_resolution=2
    s=c.splines.new('POLY');s.points.add(len(points)-1)
    for p,co in zip(s.points,points):p.co=(*co,1)
    o=bpy.data.objects.new(name,c);bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active=o;o.select_set(True)
    bpy.ops.object.convert(target='MESH');o=bpy.context.object
    o.select_set(False)
    return finish(o,name,m,parent)

# Tailored torso: broad shoulders taper to a relaxed hem; open overshirt and placket.
box('CottonTshirt',(0,-.010,1.235),(.37,.170,.47),shirt,bevel=.045)
for side in [-1,1]:
    p=box('OvershirtPanel',(.112*side,0,1.235),(.19,.275,.50),jacket,bevel=.045)
    lapel=box('FoldedCollar',(.065*side,-.143,1.435),(.088,.028,.12),jacket,bevel=.01)
    lapel.rotation_euler.y=math.radians(23*side)
    box('ChestPocket',(.128*side,-.15,1.32),(.105,.023,.115),seam,bevel=.008)
    box('PocketFlap',(.128*side,-.168,1.365),(.109,.016,.030),jacket,bevel=.004)
    ell('PocketSnap',(.128*side,-.18,1.364),(.009,.005,.009),metal)
    tube('HemStitch',[(.035*side,-.142,1.021),(.175*side,-.125,1.021)],.0025,shirt,body)
for z in [1.08,1.19,1.30]:ell('ShirtButton',(-.031,-.153,z),(.008,.004,.008),metal)
segment('Neck',(0,0,1.435),(0,0,1.575),.060,.067,skin,head)

# Face with a jaw, cheeks, nose bridge, eyelids and brows; all real geometry.
ell('Cranium',(0,.002,1.648),(.098,.084,.133),skin,head)
ell('Jaw',(0,-.018,1.578),(.074,.066,.066),skin,head)
ell('Chin',(0,-.049,1.550),(.042,.033,.025),skin,head)
for side in [-1,1]:
    ell('Ear',(.099*side,.0,1.635),(.017,.024,.035),skin,head)
    ell('EarConcha',(.111*side,-.012,1.635),(.005,.010,.017),skin_shadow,head)
    ell('EyeSocket',(.039*side,-.071,1.666),(.028,.012,.017),skin_shadow,head)
    ell('Eye',(.039*side,-.078,1.666),(.020,.008,.008),white,head)
    ell('Iris',(.038*side,-.087,1.665),(.0065,.003,.007),iris,head)
    tube('UpperEyelid',[(.018*side,-.080,1.670),(.037*side,-.086,1.676),(.061*side,-.074,1.670)],.0038,skin,head)
    tube('Brow',[(.017*side,-.078,1.692),(.039*side,-.084,1.697),(.064*side,-.071,1.690)],.0045,hair,head)
ell('NoseBridge',(0,-.081,1.645),(.012,.016,.029),skin,head)
ell('NoseTip',(0,-.101,1.627),(.018,.017,.011),skin,head)
for side in [-1,1]:ell('Nostril',(.013*side,-.103,1.621),(.005,.005,.003),skin_shadow,head)
tube('Mouth',[(-.027,-.075,1.589),(0,-.084,1.586),(.027,-.075,1.589)],.0025,skin_shadow,head)
ell('LowerLip',(0,-.078,1.582),(.021,.006,.005),skin,head)
# Sculpted swept hair cap; several broad locks keep the silhouette readable on CCTV.
ell('HairCap',(0,.013,1.720),(.101,.086,.068),hair,head)
for i in range(7):
    x=-.074+i*.024
    lock=ell('SweptLock',(x,-.055,1.728-abs(x)*.25),(.028,.040,.052),hair,head)
    lock.rotation_euler.y=-.5
for side in [-1,1]:
    ell('Sideburn',(.089*side,.008,1.678),(.013,.057,.043),hair,head)

# Sleeves and hands, slightly asymmetric: a phone held in the right hand.
for side,pivot in [(1,la),(-1,ra)]:
    shoulder=(side*.245,0,1.401);elbow=(side*.290,-.014,1.161)
    wrist=(side*.300,-.080,1.005)
    ell('Shoulder',shoulder,(.087,.102,.105),jacket,pivot)
    segment('UpperSleeve',elbow,shoulder,.070,.082,jacket,pivot)
    ell('Elbow',elbow,(.068,.068,.079),jacket,pivot)
    segment('LowerSleeve',wrist,elbow,.053,.069,jacket,pivot)
    segment('Cuff',(side*.300,-.080,.988),(side*.300,-.070,1.039),.059,.061,seam,pivot)
    ell('Hand',(side*.300,-.081,.962),(.038,.026,.060),skin,pivot)
    for i in range(4):
        x=side*.300+(i-1.5)*.014
        ell('Finger',(x,-.086,.928),(.008,.013,.033-abs(i-1.5)*.004),skin,pivot,12,8)
    ell('Thumb',(side*.266,-.101,.967),(.014,.018,.034),skin,pivot,16,10)
box('PhoneCase',(-.302,-.118,.97),(.076,.016,.140),rubber,ra,bevel=.008)
box('PhoneScreen',(-.302,-.128,.975),(.061,.003,.112),screen,ra,bevel=.004)

# Trousers, creases, low sneakers. Pivot geometry keeps knees covered while walking.
box('TrouserSeat',(0,0,.922),(.335,.239,.19),pants,root,bevel=.065)
for side,pivot in [(1,ll),(-1,rl)]:
    x=side*.105
    segment('TrouserThigh',(x,0,.51),(x,0,.945),.077,.095,pants,pivot)
    ell('Knee',(x,0,.51),(.072,.072,.083),pants,pivot)
    segment('TrouserShin',(x,.007,.14),(x,0,.52),.057,.074,pants,pivot)
    for z in [.19,.49,.55]:
        tube('DenimFold',[(x-.043,-.044,z),(x,-.070,z+.011),(x+.041,-.044,z+.018)],.004,seam,pivot)
    box('SneakerSole',(x,-.039,.037),(.155,.293,.062),sole,pivot,bevel=.026)
    box('SneakerUpper',(x,-.041,.097),(.145,.269,.104),rubber,pivot,bevel=.037)
    for y in [-.064,-.041,-.017]:
        tube('Laces',[(x-.043,y,.144),(x+.043,y-.01,.144)],.0035,sole,pivot)

# Crossbody bag and strap add an unmistakable civilian silhouette.
box('CrossbodyBag',(.200,.025,1.030),(.195,.160,.225),pants,body,bevel=.035)
box('BagFrontPocket',(.20,-.064,1.02),(.148,.021,.145),rubber,body,bevel=.014)
tube('ShoulderStrap',[(-.19,.09,1.43),(-.19,-.12,1.43),(-.08,-.177,1.30),(.06,-.182,1.16),(.20,-.09,1.10)],.017,rubber,body)
box('BagZip',(.20,-.079,1.077),(.124,.008,.008),metal,body,bevel=.002)

# Consolidate per pivot: six animated mesh groups, shared material slots.
for pivot in [head,la,ra,ll,rl,body,root]:
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.parent==pivot]
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:o.select_set(True)
    if meshes:
        bpy.context.view_layer.objects.active=meshes[0]
        bpy.ops.object.join()
        bpy.context.object.name=pivot.name.replace('Pivot','')+'_Mesh'

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'FirstGuest.fbx'),use_selection=True,
    object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,
    add_leaf_bones=False,bake_anim=False,path_mode='AUTO')

# A reproducible studio render, separate from the exported game object.
floor=mat('StudioFloor',(.052,.063,.068))
box('StudioFloor',(0,0,-.045),(200,200,.05),floor,None,0)
world=bpy.context.scene.world
world.color=(.15,.15,.15)
def area(name,pos,power,color,size):
    bpy.ops.object.light_add(type='AREA',location=pos)
    o=bpy.context.object;o.name=name;o.data.energy=power;o.data.color=color;o.data.shape='DISK';o.data.size=size
    o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
area('Key',(-3,-4,5),480,(1,.83,.69),4)
area('Fill',(3,-2,3),330,(.70,.84,1),3)
area('Rim',(1,3,4),600,(.83,1,.91),3)
bpy.ops.object.camera_add(location=(2.55,-5.8,2.6))
cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.91))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO';cam.data.ortho_scale=2.24
scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=900;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=os.path.join(SOURCE,'FirstGuest_Studio.png')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'FirstGuest.blend'))
bpy.ops.render.render(write_still=True)
print('FIRST_GUEST_EXPORTED',OUT)
