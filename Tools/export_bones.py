import bpy,json,os,sys,argparse
from pathlib import Path
from mathutils import Vector
parser=argparse.ArgumentParser(description='Export source skeleton meshes for the Unity operating theatre.')
parser.add_argument('--source',required=True)
parser.add_argument('--output',default=str(Path(__file__).resolve().parents[1]/'Assets/BoneAssembly/Resources/SkeletonData.json'))
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
bpy.ops.wm.open_mainfile(filepath=str(Path(args.source).resolve()))
out=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 # Preserve the actual source mesh and world transforms, without rig animation.
 m=o.data; m.calc_loop_triangles(); vs=[o.matrix_world@v.co for v in m.vertices]
 lo=[min(v[i] for v in vs) for i in range(3)]; hi=[max(v[i] for v in vs) for i in range(3)]; c=[(lo[i]+hi[i])/2 for i in range(3)]
 idx=int(o.name.rsplit('.',1)[-1]) if o.name.rsplit('.',1)[-1].isdigit() else 0
 out.append({'id':idx,'center':{'x':c[0]*.105,'y':-c[1]*.105,'z':(c[2]-8.9)*.105},'sourceCenter':{'x':c[0],'y':c[1],'z':c[2]},'vertices':[{'x':(v.x-c[0])*.105,'y':-(v.y-c[1])*.105,'z':(v.z-c[2])*.105} for v in vs], 'triangles':[i for t in m.loop_triangles for i in (t.vertices[0],t.vertices[2],t.vertices[1])]})
Path(args.output).parent.mkdir(parents=True,exist_ok=True)
with open(args.output,'w',encoding='utf-8') as stream:
 json.dump({'bones':out},stream,separators=(',',':'))
print('EXPORTED',len(out),'meshes',sum(len(b['vertices']) for b in out),'vertices')
