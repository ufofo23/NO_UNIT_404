"""Prepare the reference-based Rodin mesh for Unity, keeping its source recoverable."""
import bpy, math, pathlib, json
from mathutils import Vector

ROOT=pathlib.Path(__file__).resolve().parents[1]
SOURCE=ROOT/'Docs/Art/ReferenceGuest'
OUT=ROOT/'Assets/_Project/Resources/NO404/Characters/FirstGuest'
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE/'base_basic_pbr.glb'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.join(); mesh=bpy.context.object;mesh.name='ReferenceWorker'
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
points=[mesh.matrix_world @ v.co for v in mesh.data.vertices]
low=Vector(tuple(min(p[i] for p in points) for i in range(3)))
high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
scale=1.80/(high.z-low.z)
for v in mesh.data.vertices:
    v.co=(mesh.matrix_world @ v.co-Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z)))*scale
mesh.matrix_world.identity()
print('MESH',len(mesh.data.vertices),'faces',len(mesh.data.polygons),'source bounds',tuple(low),tuple(high))
for p in mesh.data.polygons:p.use_smooth=True

def project_reference():
    """Bake the user's four views onto the generated topology, retaining unseen texels.
    No synthesized replacement face: front/back/side projections use the supplied photo.
    """
    material=mesh.data.materials[0];material.name='ReferenceWorker'
    nodes=material.node_tree.nodes;links=material.node_tree.links
    original=next(n for n in nodes if n.type=='TEX_IMAGE' and n.image and 'diffuse' in n.image.name)
    photo=bpy.data.images.load(str(ROOT/'NO_UNIT_404_ADULT_CHARACTER_REFERENCES/ChatGPT Image 2026년 9월 8일 오후 06_52_15.png'))
    normal=nodes.new('ShaderNodeNewGeometry')
    def math_node(op,a,b=None):
        n=nodes.new('ShaderNodeMath');n.operation=op
        if isinstance(a,(int,float)):n.inputs[0].default_value=a
        else:links.new(a,n.inputs[0])
        if b is not None:
            if isinstance(b,(int,float)):n.inputs[1].default_value=b
            else:links.new(b,n.inputs[1])
        return n.outputs[0]
    def scale_color(color,weight):
        n=nodes.new('ShaderNodeVectorMath');n.operation='SCALE'
        links.new(color,n.inputs[0]);links.new(weight,n.inputs[3]);return n.outputs[0]
    original_uv=mesh.data.uv_layers.active.name
    olduv=nodes.new('ShaderNodeUVMap');olduv.uv_map=original_uv;links.new(olduv.outputs[0],original.inputs['Vector'])
    # Model and source sheet share the same upright neutral stance. Front and back
    # use X; profiles use depth. Centres are the anatomical midlines in the sheet.
    # Profiles occlude the thigh with the hands. Projecting them blindly would paint
    # a second glove on the trousers. Retain Rodin's visibility-resolved UV texture
    # on side-facing surfaces and use the unobstructed front/back for fine detail.
    views=[('Front',(0,-1,0),240,1,0),('Back',(0,1,0),899,-1,0)]
    colors=[];weights=[]
    for name,axis,center,signx,signy in views:
        uv=mesh.data.uv_layers.new(name='Projection'+name)
        for loop in mesh.data.loops:
            p=mesh.data.vertices[loop.vertex_index].co
            uv.data[loop.index].uv=((center+(signx*p.x+signy*p.y)*526.1)/1448,
                                   1-(994-p.z*526.1)/1086)
        coords=nodes.new('ShaderNodeUVMap');coords.uv_map=uv.name
        tex=nodes.new('ShaderNodeTexImage');tex.image=photo;tex.extension='CLIP'
        links.new(coords.outputs[0],tex.inputs['Vector'])
        dot=nodes.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT'
        links.new(normal.outputs['Normal'],dot.inputs[0]);dot.inputs[1].default_value=axis
        weight=math_node('POWER',math_node('MAXIMUM',dot.outputs['Value'],0),2)
        separate=nodes.new('ShaderNodeSeparateColor');links.new(tex.outputs['Color'],separate.inputs[0])
        darkest=math_node('MINIMUM',math_node('MINIMUM',separate.outputs[0],separate.outputs[1]),separate.outputs[2])
        foreground=math_node('LESS_THAN',darkest,.50)
        weight=math_node('MULTIPLY',weight,foreground)
        colors.append(tex.outputs['Color']);weights.append(weight)
    total=nodes.new('ShaderNodeValue');total.outputs[0].default_value=.0001
    summed=scale_color(original.outputs['Color'],total.outputs[0]);denom=total.outputs[0]
    for color,weight in zip(colors,weights):
        add=nodes.new('ShaderNodeVectorMath');add.operation='ADD'
        links.new(summed,add.inputs[0]);links.new(scale_color(color,weight),add.inputs[1]);summed=add.outputs[0]
        denom=math_node('ADD',denom,weight)
    result=scale_color(summed,math_node('DIVIDE',1,denom))
    emission=nodes.new('ShaderNodeEmission');links.new(result,emission.inputs[0])
    output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
    links.new(emission.outputs[0],output.inputs['Surface'])
    bake=bpy.data.images.new('Worker_BaseColor',width=4096,height=4096,alpha=False)
    target=nodes.new('ShaderNodeTexImage');target.image=bake;nodes.active=target
    mesh.data.uv_layers.active_index=0
    bpy.context.view_layer.objects.active=mesh
    bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True)
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1
    scene.render.bake.margin=12;scene.render.bake.use_clear=True
    bpy.ops.object.bake(type='EMIT')
    bake.filepath_raw=str(OUT/'Worker_BaseColor.png');bake.file_format='PNG';bake.save()
    principled=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    links.new(principled.outputs[0],output.inputs['Surface'])
    links.new(target.outputs['Color'],principled.inputs['Base Color'])
    links.new(olduv.outputs[0],target.inputs['Vector'])
    # The trial normal map is coarse; use a subtle amount rather than amplifying it.
    for node in nodes:
        if node.type=='NORMAL_MAP':node.inputs['Strength'].default_value=.35
    for name in ['Roughness','Metallic']:
        for link in list(principled.inputs[name].links):links.remove(link)
    principled.inputs['Roughness'].default_value=.82;principled.inputs['Metallic'].default_value=0
    # Only the baked material nodes survive into the source and exported FBX.
    keep={principled,output,olduv,target}
    for node in list(nodes):
        if node not in keep:nodes.remove(node)
    for uv in list(mesh.data.uv_layers)[1:]:mesh.data.uv_layers.remove(uv)

def rig_character():
    data=bpy.data.armatures.new('WorkerSkeleton')
    rig=bpy.data.objects.new('WorkerRig',data);bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);mesh.select_set(False)
    bpy.ops.object.mode_set(mode='EDIT')
    def bone(name,head,tail,parent=None):
        b=data.edit_bones.new(name);b.head=head;b.tail=tail
        if parent:b.parent=data.edit_bones[parent]
    bone('BodyPivot',(0,0,.94),(0,0,1.1))
    bone('SpinePivot',(0,0,1.1),(0,0,1.3),'BodyPivot')
    bone('ChestPivot',(0,0,1.3),(0,0,1.49),'SpinePivot')
    bone('HeadPivot',(0,0,1.49),(0,0,1.76),'ChestPivot')
    for side,s in [('Left',1),('Right',-1)]:
        bone(side+'ArmPivot',(s*.195,0,1.43),(s*.28,-.015,1.13),'ChestPivot')
        bone(side+'ForeArmPivot',(s*.28,-.015,1.13),(s*.318,-.025,.89),side+'ArmPivot')
        bone(side+'HandPivot',(s*.318,-.025,.89),(s*.325,-.025,.75),side+'ForeArmPivot')
        bone(side+'LegPivot',(s*.10,0,.94),(s*.125,0,.51),'BodyPivot')
        bone(side+'ShinPivot',(s*.125,0,.51),(s*.13,0,.115),side+'LegPivot')
        bone(side+'FootPivot',(s*.13,0,.115),(s*.13,-.15,.055),side+'ShinPivot')
    bpy.ops.object.mode_set(mode='OBJECT')
    groups={b.name:mesh.vertex_groups.new(name=b.name) for b in data.bones}
    def smooth(a,b,x):
        t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
    for v in mesh.data.vertices:
        x,y,z=v.co;side='Left' if x>=0 else 'Right'
        weights={}
        if z<.98 and abs(x)<.245:
            hips=smooth(.85,.98,z)
            # The entire shoe follows one rigid foot bone. Blend only above
            # the shoe collar, inside the trouser cuff, to avoid rubbery soles.
            shin=1-smooth(.43,.59,z);foot=1-smooth(.16,.24,z)
            weights={'BodyPivot':hips,side+'LegPivot':(1-hips)*(1-shin),
                     side+'ShinPivot':(1-hips)*shin*(1-foot),side+'FootPivot':(1-hips)*shin*foot}
        else:
            arm=smooth(.185,.25,abs(x))*(1-smooth(1.44,1.52,z))
            fore=1-smooth(1.06,1.20,z);hand=1-smooth(.86,.94,z)
            weights[side+'ArmPivot']=arm*(1-fore)
            weights[side+'ForeArmPivot']=arm*fore*(1-hand)
            weights[side+'HandPivot']=arm*fore*hand
            head=smooth(1.46,1.56,z);chest=smooth(1.15,1.34,z);spine=smooth(.99,1.15,z)
            weights['HeadPivot']=(1-arm)*head
            weights['ChestPivot']=(1-arm)*(1-head)*chest
            weights['SpinePivot']=(1-arm)*(1-head)*(1-chest)*spine
            weights['BodyPivot']=(1-arm)*(1-head)*(1-chest)*(1-spine)
        keep=sorted([(n,w) for n,w in weights.items() if w>.0001],key=lambda a:-a[1])[:4]
        total=sum(w for _,w in keep)
        for name,weight in keep:groups[name].add([v.index],weight/total,'REPLACE')
    mesh.parent=rig
    modifier=mesh.modifiers.new('SkinDeformation','ARMATURE');modifier.object=rig
    return rig

project_reference()
rig=rig_character()
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);rig.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'FirstGuest.fbx'),use_selection=True,
    object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,
    add_leaf_bones=False,bake_anim=False,path_mode='AUTO')

def studio():
    scene=bpy.context.scene
    scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.render.resolution_x=1000;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
    scene.world.color=(.18,.18,.18)
    scene.view_settings.view_transform='Standard'
    scene.view_settings.look='Medium High Contrast'
    def area(name,pos,power,size):
        bpy.ops.object.light_add(type='AREA',location=pos)
        o=bpy.context.object;o.name=name;o.data.energy=power;o.data.size=size
        o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
    area('Key',(-3,-4,5),350,4);area('Fill',(3,-2,3),250,4);area('Rim',(1,3,4),450,3)
    bpy.ops.object.camera_add(location=(2,-6,2.1));cam=bpy.context.object
    cam.rotation_euler=(Vector((0,0,.93))-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.type='ORTHO';cam.data.ortho_scale=2.16;scene.camera=cam
    scene.render.image_settings.file_format='PNG'
    scene.render.filepath=str(SOURCE/'ReferenceGuest_Final.png')
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'ReferenceGuest_Working.blend'))

studio()
