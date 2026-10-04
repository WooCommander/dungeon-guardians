from pathlib import Path
import json,struct,copy,math
import numpy as np
import character_source as ch
ROOT=Path(__file__).resolve().parent
ch.ROOT=ROOT/'assets'

def asset(name):
 c=ch.Character(name);c.joint('origin',None,[0,0,0])
 for n,col in [('sand',(.52,.36,.20)),('light',(.64,.47,.28)),('dark',(.23,.25,.25)),('wood',(.28,.14,.065)),('rope',(.60,.40,.18)),('metal',(.14,.17,.18)),('gold',(.95,.59,.09)),('black',(.07,.08,.075)),('teal',(.08,.31,.34))]:c.mat(n,col,.65 if n=='gold' else .25 if n=='metal' else 0)
 c.mat('fire',(1,.39,.025),emission=[1,.28,.015]);c.mat('core',(1,.84,.25),emission=[1,.8,.15]);c.mat('rune',(.1,.7,.7),emission=[.04,.4,.4])
 return c

def part(c,n,pos,size,mat='sand',p=.22,rot=0):c.part(n,0,pos,size,mat,p,rot)
assets={}
c=asset('block_diggable')
for y in [0,1]:
 for x in [0,1]:part(c,'brick',[(x-.5)*.49,(y+.5)*.25,0],[.475,.24,.75],'light' if (x+y)%2 else 'sand')
assets[c.name]=c
c=asset('block_solid');part(c,'foundation',[0,.25,0],[.99,.5,.78],'dark');part(c,'edge',[0,.46,.01],[1,.07,.81],'metal');assets[c.name]=c
c=asset('ladder_section')
for x in [-.28,.28]:part(c,'rail',[x,.5,0],[.075,1.06,.08],'wood',.4)
for y in [.12,.37,.62,.87]:part(c,'rung',[0,y,.02],[.64,.055,.075],'rope',.5)
assets[c.name]=c
c=asset('rope_section');part(c,'rope',[0,0,0],[1.02,.055,.065],'rope',.75);assets[c.name]=c
c=asset('gold');part(c,'ingot',[0,.09,0],[.30,.18,.19],'gold',.37);assets[c.name]=c
c=asset('torch');part(c,'mount',[0,.12,-.1],[.18,.30,.10],'metal');part(c,'handle',[0,.23,0],[.06,.45,.06],'wood',.6);part(c,'basket',[0,.44,0],[.18,.13,.16],'metal',.35);part(c,'flame',[0,.66,0],[.18,.40,.15],'fire',.85);part(c,'hot_core',[0,.61,.07],[.09,.24,.07],'core',.9);assets[c.name]=c
for opened in [False,True]:
 c=asset('door_open' if opened else 'door_closed')
 part(c,'recess',[0,.85,-.08],[1.08,1.70,.12],'black')
 for x in [-.63,.63]:
  for i in range(5):part(c,'jamb',[x,.17+i*.34,0],[.25,.32,.35],'light')
 part(c,'lintel',[0,1.80,0],[1.53,.24,.38],'light')
 if not opened:
  for i in range(5):part(c,'plank',[(i-2)*.19,.83,.04],[.18,1.63,.12],'wood')
  for y in [.3,1.35]:part(c,'strap',[0,y,.12],[.98,.08,.05],'metal')
  part(c,'lock',[.24,.78,.16],[.13,.19,.08],'gold')
 else:part(c,'portal',[0,.85,-.005],[.94,1.6,.04],'teal')
 assets[c.name]=c
c=asset('altar');part(c,'base',[0,.09,0],[.85,.18,.70],'dark');part(c,'step',[0,.25,0],[.65,.16,.5],'sand');part(c,'top',[0,.37,0],[.76,.09,.6],'light');part(c,'rune',[0,.28,.26],[.20,.08,.02],'rune');assets[c.name]=c
c=asset('column')
for i in range(7):part(c,'shaft',[0,.22+i*.44,0],[.46,.42,.45],'dark')
for y in [.08,3.1]:part(c,'cap',[0,y,0],[.73,.16,.65],'teal')
assets[c.name]=c
c=asset('rubble');
for i,(x,y,z,s) in enumerate([(-.15,.08,0,.17),(.08,.06,.03,.12),(.22,.04,-.02,.08)]):part(c,'chip',[x,y,z],[s,s,s],'sand',.3,rot=i*.6)
assets[c.name]=c
for c in assets.values():c.export([])
for g in [False,True]:
 c,clips=ch.make(g);assets[c.name]=c;c.export(clips)

# Load/merge glTF while preserving skin, animation, and asset-relative hierarchy.
def load(p):
 b=p.read_bytes();n=struct.unpack_from('<I',b,12)[0];return json.loads(b[20:20+n]),b[28+n:]
def save(doc,blob,path):
 doc['buffers']=[{'byteLength':len(blob)}];j=json.dumps(doc,separators=(',',':')).encode();j+=b' '*((-len(j))%4);blob+=b'\0'*((-len(blob))%4)
 path.write_bytes(struct.pack('<III',0x46546c67,2,28+len(j)+len(blob))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(blob),0x004e4942)+blob)
doc={'asset':{'version':'2.0','generator':'Dungeon modular scene 0.1'},'scene':0,'scenes':[{'nodes':[]}],'nodes':[],'meshes':[],'materials':[],'accessors':[],'bufferViews':[],'skins':[],'animations':[]};blob=bytearray();placements=[];render=[]
def place(name,pos,scale=1,angle=0):
 global blob
 d,b=load(ROOT/'assets'/(name+'.glb'));o={k:len(doc[k]) for k in ['nodes','meshes','materials','accessors','bufferViews','skins']};start=len(blob);blob.extend(b)
 for v in d['bufferViews']:v['byteOffset']=v.get('byteOffset',0)+start;doc['bufferViews'].append(v)
 for a in d['accessors']:a['bufferView']+=o['bufferViews'];doc['accessors'].append(a)
 doc['materials']+=d['materials']
 for mesh in d['meshes']:
  for p in mesh['primitives']:
   p['attributes']={k:v+o['accessors'] for k,v in p['attributes'].items()};p['indices']+=o['accessors'];p['material']+=o['materials']
  doc['meshes'].append(mesh)
 for skin in d['skins']:
  skin['joints']=[i+o['nodes'] for i in skin['joints']];skin['skeleton']+=o['nodes'];skin['inverseBindMatrices']+=o['accessors'];doc['skins'].append(skin)
 for n in d['nodes']:
  if 'children'in n:n['children']=[i+o['nodes'] for i in n['children']]
  for k in ['mesh','skin']:
   if k in n:n[k]+=o[k+'es' if k=='mesh' else 'skins']
  doc['nodes'].append(n)
 for anim in d.get('animations',[]):
  anim['name']=name+'/'+anim['name']
  for sam in anim['samplers']:sam['input']+=o['accessors'];sam['output']+=o['accessors']
  for chan in anim['channels']:chan['target']['node']+=o['nodes']
  doc['animations'].append(anim)
 idx=len(doc['nodes']);doc['nodes'].append({'name':name+'_placement','translation':pos,'scale':[scale]*3,'rotation':[0,math.sin(angle/2),0,math.cos(angle/2)],'children':[o['nodes']]});doc['scenes'][0]['nodes'].append(idx)
 placements.append({'asset':name,'position':pos,'scale':scale,'yaw':angle})
 r=np.array([[math.cos(angle),0,math.sin(angle)],[0,1,0],[-math.sin(angle),0,math.cos(angle)]])
 for nm,bone,p,f,m in assets[name].parts:
  mat=assets[name].materials[m];render.append((p@r.T*scale+pos,f,mat['pbrMetallicRoughness']['baseColorFactor'][:3],mat.get('emissiveFactor')))
# Side-view playable miniature; front is +Z. Props placed behind path.
for x in range(-5,6):place('block_solid',[x,0,0])
for x in [-5,-4,-3,-2,1,2,3,4,5]:place('block_diggable',[x,2,0])
for x in [-5,-4,-3,2,3,4,5]:place('block_diggable',[x,4,0])
for y in [.5,1.5]:place('ladder_section',[-3,y,.48])
for y in [2.5,3.5]:place('ladder_section',[3,y,.48])
for x in [-2,-1,0,1]:place('rope_section',[x,4.05,0])
place('door_closed',[4.25,4.5,-.24],.72)
place('explorer',[-.85,.5,.03],.64,math.pi/5)
place('guardian',[1.8,2.515,.03],.67,-math.pi/7)
place('altar',[-4,.5,-.10])
for x,y in [(-2,.5),(1,.5),(3,.5),(-4,2.5),(2,2.5),(-4,4.5),(2,4.5)]:place('gold',[x,y,.18])
for x,y in [(-4.6,.65),(0,1),(4.7,2.7)]:place('torch',[x,y,-.48],.8)
for x in [-4.5,-.5,4.5]:place('column',[x,.5,-2.1],1.5)
for x in [-1.5,4.3]:place('rubble',[x,.5,.1])
# Real glTF punctual lights. Emission does not itself illuminate the scene.
lights=[{'type':'directional','color':[1,.85,.68],'intensity':2.2},{'type':'point','color':[1,.43,.10],'intensity':25,'range':3},{'type':'point','color':[.12,.55,.8],'intensity':40,'range':9}]
doc['extensionsUsed']=['KHR_lights_punctual'];doc['extensions']={'KHR_lights_punctual':{'lights':lights}}
for i,(p,rot) in enumerate([([0,7,5],[-.35,.25,0,.9027735]),([-3.7,1.4,.1],[0,0,0,1]),([0,3,-1.5],[0,0,0,1])]):
 rot=np.array(rot);rot=rot/np.linalg.norm(rot);idx=len(doc['nodes']);doc['nodes'].append({'name':'light_'+str(i),'translation':p,'rotation':rot.tolist(),'extensions':{'KHR_lights_punctual':{'light':i}}});doc['scenes'][0]['nodes'].append(idx)
doc['cameras']=[{'name':'Side view','type':'orthographic','orthographic':{'xmag':6.5,'ymag':3.65,'znear':.1,'zfar':100}}];idx=len(doc['nodes']);doc['nodes'].append({'name':'Camera','camera':0,'translation':[0,3,18]});doc['scenes'][0]['nodes'].append(idx)
save(doc,blob,ROOT/'dungeon_scene.glb');(ROOT/'layout.json').write_text(json.dumps(placements,indent=2))
# Software rasterization of actual mesh triangles: orthographic, z-buffer, flat diffuse shading.
from PIL import Image,ImageDraw
W,H=1500,900
img=np.zeros((H,W,3),dtype=np.uint8)
for y in range(H):img[y,:,:]=np.array([15,31,39])*(.75+.4*y/H)
zbuf=np.full((H,W),-np.inf,dtype=np.float32)
light=np.array([-.4,.75,1]);light/=np.linalg.norm(light)
# Slight vertical view angle shows platform depth while maintaining side-on legibility.
angle=.08; ca,sa=math.cos(angle),math.sin(angle)
for p,f,col,em in render:
 pp=p.copy();pp[:,1]=ca*p[:,1]-sa*p[:,2];pp[:,2]=sa*p[:,1]+ca*p[:,2]
 screen=np.column_stack((W/2+pp[:,0]*116,H-95-pp[:,1]*116,pp[:,2]))
 for tri in f:
  v=screen[tri];mn=np.floor(v[:,:2].min(axis=0)).astype(int);mx=np.ceil(v[:,:2].max(axis=0)).astype(int)
  x0,y0=np.maximum(mn,[0,0]);x1,y1=np.minimum(mx,[W-1,H-1])
  if x1<x0 or y1<y0:continue
  a,b,c=v;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
  if abs(den)<1e-7:continue
  yy,xx=np.mgrid[y0:y1+1,x0:x1+1];xx=xx+.5;yy=yy+.5
  u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
  vv=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;ww=1-u-vv;z=u*a[2]+vv*b[2]+ww*c[2]
  mask=(u>=-1e-6)&(vv>=-1e-6)&(ww>=-1e-6)&(z>zbuf[y0:y1+1,x0:x1+1])
  if not mask.any():continue
  pv=p[tri];n=np.cross(pv[1]-pv[0],pv[2]-pv[0]);n/=max(np.linalg.norm(n),1e-10)
  color=np.array(col)*(.38+.62*max(0,n@light));
  if em:color=np.maximum(color,np.array(col)*.95)
  color=np.clip(color*255,0,255).astype(np.uint8)
  img[y0:y1+1,x0:x1+1][mask]=color;zbuf[y0:y1+1,x0:x1+1][mask]=z[mask]
out=Image.fromarray(img);draw=ImageDraw.Draw(out);draw.text((35,25),'DUNGEON / MODULAR 3D SCENE / PROTOTYPE 0.1',fill=(211,181,124));draw.text((35,50),'Actual mesh preview - lighting differs from the target engine',fill=(146,171,180));out.save(ROOT/'preview.png')
print('Created scene with',len(placements),'placements;',len(doc['animations']),'animation clips;',len(blob),'binary bytes')
