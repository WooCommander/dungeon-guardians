"""Procedural prototype characters. Python 3 + numpy. Outputs glTF 2.0 GLB."""
import json, math, struct, pathlib
import numpy as np
ROOT=pathlib.Path(__file__).resolve().parent

def rounded(size,power=0.45, rings=10, sides=16):
    # Superellipsoid, with separate poles, consistent outward triangle winding.
    def sp(x): return math.copysign(abs(x)**power,x)
    pts=[[0,-size[1]/2,0]]
    for i in range(1,rings):
        a=-math.pi/2+math.pi*i/rings
        for j in range(sides):
            b=2*math.pi*j/sides
            pts.append([size[0]/2*sp(math.cos(a))*sp(math.cos(b)),size[1]/2*sp(math.sin(a)),size[2]/2*sp(math.cos(a))*sp(math.sin(b))])
    pts.append([0,size[1]/2,0]); f=[]
    for j in range(sides): f.append([0,1+j,1+(j+1)%sides])
    for i in range(rings-2):
        for j in range(sides):
            a=1+i*sides+j;b=1+i*sides+(j+1)%sides;c=a+sides;d=b+sides
            f.extend([[a,c,b],[b,c,d]])
    top=len(pts)-1; base=1+(rings-2)*sides
    for j in range(sides):f.append([base+j,top,base+(j+1)%sides])
    p=np.array(pts,float); f=np.array(f,int)
    # Ensure winding is outward.
    for k,t in enumerate(f):
        if np.dot(np.cross(p[t[1]]-p[t[0]],p[t[2]]-p[t[0]]),p[t].mean(axis=0))<0:f[k]=t[::-1]
    return p,f

class Character:
    def __init__(self,name):
        self.name=name;self.parts=[]; self.joints=[]; self.world=[]; self.materials=[]; self.matids={};self.tris=0
    def joint(self,name,parent,pos):
        i=len(self.joints);self.joints.append((name,parent,np.array(pos,float)));self.world.append(np.array(pos,float)+(self.world[parent] if parent is not None else 0));return i
    def mat(self,name,col,metal=0,emission=None):
        self.matids[name]=len(self.materials)
        m={'name':name,'pbrMetallicRoughness':{'baseColorFactor':[*col,1],'metallicFactor':metal,'roughnessFactor':.75}}
        if emission:m['emissiveFactor']=emission
        self.materials.append(m)
    def part(self,name,bone,pos,size,mat,power=.45,rot=0):
        p,f=rounded(size,power)
        r=np.array([[math.cos(rot),-math.sin(rot),0],[math.sin(rot),math.cos(rot),0],[0,0,1]])
        p=p@r.T+np.array(pos)+self.world[bone]
        self.parts.append((name,bone,p,f,self.matids[mat]));self.tris+=len(f)
    def export(self,clips):
        blob=bytearray();views=[];access=[]
        def acc(a,typ,ct=5126,bounds=False):
            a=np.asarray(a,dtype={5126:'<f4',5123:'<u2',5125:'<u4'}[ct]);
            while len(blob)%4:blob.append(0)
            offset=len(blob);blob.extend(a.tobytes());views.append({'buffer':0,'byteOffset':offset,'byteLength':a.nbytes})
            x={'bufferView':len(views)-1,'componentType':ct,'count':len(a),'type':typ}
            if bounds:
                b=a.reshape(len(a),-1);x['min']=b.min(axis=0).tolist();x['max']=b.max(axis=0).tolist()
            access.append(x);return len(access)-1
        nodes=[{'name':self.name,'children':[]}]
        for i,(n,par,pos) in enumerate(self.joints):
            nodes.append({'name':n,'translation':pos.tolist(),'children':[]})
            nodes[0 if par is None else par+1]['children'].append(i+1)
        inv=[]
        for w in self.world:
            m=np.eye(4);m[:3,3]=-w;inv.append(m.T.flatten())
        skin={'name':'Rigid segmented skeleton','joints':list(range(1,len(self.joints)+1)),'skeleton':1,'inverseBindMatrices':acc(inv,'MAT4')}
        prim=[]
        for name,bone,p,f,mi in self.parts:
            n=np.zeros_like(p)
            for t in f:
                nn=np.cross(p[t[1]]-p[t[0]],p[t[2]]-p[t[0]])
                for v in t:n[v]+=nn
            n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-12)
            js=np.zeros((len(p),4),dtype=np.uint16);js[:,0]=bone
            ws=np.zeros((len(p),4));ws[:,0]=1
            uv=np.column_stack((.5+np.arctan2(p[:,2]-p[:,2].mean(),p[:,0]-p[:,0].mean())/(2*np.pi),(p[:,1]-p[:,1].min())/max(np.ptp(p[:,1]),1e-5)))
            prim.append({'attributes':{'POSITION':acc(p,'VEC3',bounds=True),'NORMAL':acc(n,'VEC3'),'TEXCOORD_0':acc(uv,'VEC2'),'JOINTS_0':acc(js,'VEC4',5123),'WEIGHTS_0':acc(ws,'VEC4')},'indices':acc(f.flatten(),'SCALAR',5125),'material':mi,'extras':{'part':name}})
        nodes.append({'name':self.name+'_mesh','mesh':0,'skin':0});nodes[0]['children'].append(len(nodes)-1)
        animations=[]
        for cname,duration,tracks in clips:
            sam=[];chan=[]; times=np.linspace(0,duration,25)
            ti=acc(times,'SCALAR',bounds=True)
            for bone,path,func in tracks:
                vals=[func(t/duration) for t in times]
                sam.append({'input':ti,'output':acc(vals,'VEC4' if path=='rotation' else 'VEC3'),'interpolation':'LINEAR'})
                chan.append({'sampler':len(sam)-1,'target':{'node':bone+1,'path':path}})
            animations.append({'name':cname,'samplers':sam,'channels':chan})
        doc={'asset':{'version':'2.0','generator':'Dungeon Guardians procedural prototype v0.1'},'scene':0,'scenes':[{'nodes':[0]}],'nodes':nodes,'meshes':[{'name':self.name,'primitives':prim}],'skins':[skin],'materials':self.materials,'buffers':[{'byteLength':len(blob)}],'bufferViews':views,'accessors':access,'animations':animations}
        b=json.dumps(doc,separators=(',',':')).encode();b+=b' '*((-len(b))%4);blob+=b'\0'*((-len(blob))%4)
        out=struct.pack('<III',0x46546c67,2,12+8+len(b)+8+len(blob))+struct.pack('<II',len(b),0x4e4f534a)+b+struct.pack('<II',len(blob),0x004e4942)+blob
        (ROOT/(self.name+'.glb')).write_bytes(out)
        return {'file':self.name+'.glb','triangles':self.tris,'joints':len(self.joints),'mesh_parts':len(self.parts),'animations':[x['name'] for x in animations],'bytes':len(out)}

def q(a,axis='x'):
    r=[0.,0.,0.,math.cos(a/2)];r['xyz'.index(axis)]=math.sin(a/2);return r

def make(guard=False):
    c=Character('guardian' if guard else 'explorer')
    for n,col in [('stone',(.47,.32,.16)),('edge',(.66,.47,.25)),('dark',(.13,.08,.045)),('gold',(.93,.48,.055)),('skin',(.83,.43,.22)),('ivory',(.92,.81,.59)),('cloth',(.37,.12,.065)),('metal',(.29,.34,.38)),('white',(.97,.94,.8)),('black',(.018,.014,.012))]:c.mat(n,col,.45 if n=='metal' else 0)
    c.mat('glow',(.04,.85,.85),emission=[.03,.7,.7]);c.mat('lamp',(1,.91,.6),emission=[1,.8,.35])
    root=c.joint('root',None,[0,0,0]); hips=c.joint('hips',root,[0,.73 if guard else .78,0]);spine=c.joint('spine',hips,[0,.25,0]);head=c.joint('head',spine,[0,.46 if guard else .54,0]); arms=[];legs=[]
    for sign,side in [(-1,'L'),(1,'R')]:
        a=c.joint('upper_arm_'+side,spine,[sign*(.43 if guard else .32),.21,0]);e=c.joint('forearm_'+side,a,[sign*.09,-.27,0]);h=c.joint('hand_'+side,e,[0,-.23,.02]);arms.append((a,e,h))
        l=c.joint('thigh_'+side,hips,[sign*.20,-.06,0]);k=c.joint('shin_'+side,l,[0,-.29,0]);ft=c.joint('foot_'+side,k,[0,-.24,.065]);legs.append((l,k,ft))
    if guard:
        c.part('torso',spine,[0,.05,0],[.73,.56,.43],'stone',.24)
        c.part('chest_plate',spine,[0,.07,.235],[.5,.36,.09],'edge',.15)
        c.part('belt',hips,[0,.06,0],[.77,.12,.49],'dark')
        c.part('sash',hips,[0,-.14,.25],[.27,.37,.055],'cloth',.18)
        c.part('mask',head,[0,.06,.025],[.78,.69,.45],'edge',.2)
        for s in [-1,1]:
            c.part('eye_socket',head,[s*.185,.105,.26],[.28,.25,.075],'dark',.4)
            c.part('turquoise_eye',head,[s*.185,.105,.31],[.16,.16,.055],'glow',1)
            c.part('brow',head,[s*.19,.26,.30],[.31,.085,.09],'stone',.2,rot=s*.16)
            c.part('ear_stone',head,[s*.44,.08,0],[.18,.32,.30],'stone',.18,rot=s*.15)
        c.part('mouth',head,[0,-.17,.265],[.52,.13,.04],'dark',.15)
        for x in [-.18,-.06,.06,.18]:c.part('tooth',head,[x,-.15,.295],[.095,.09,.06],'ivory',.25)
        c.part('crest',head,[0,.49,-.02],[.26,.36,.16],'edge',.18)
        # Contrasting relief motifs remain geometry, not texture.
        for x,y,w,h in [(-.055,.49,.028,.18),(0,.58,.14,.025),(.06,.53,.025,.10),(.015,.48,.09,.025)]:c.part('crest_rune',head,[x,y,.074],[w,h,.018],'dark',.15)
        for x,y,w,h in [(-.065,.08,.025,.17),(0,.15,.15,.025),(.065,.1,.025,.10),(0,.055,.13,.025)]:c.part('chest_rune',spine,[x,y,.291],[w,h,.018],'dark',.15)
    else:
        c.part('jacket',spine,[0,.02,0],[.59,.58,.38],'gold')
        c.part('trousers',hips,[0,-.04,0],[.51,.25,.34],'dark')
        c.part('belt',hips,[0,.13,.01],[.61,.085,.4],'dark')
        c.part('buckle',hips,[0,.13,.23],[.12,.10,.04],'edge')
        for s in [-1,1]:
            c.part('backpack_strap',spine,[s*.20,.08,.205],[.065,.46,.04],'dark')
            c.part('collar',spine,[s*.075,.24,.215],[.12,.13,.055],'ivory',.3,rot=s*.35)
        for y in [-.08,.04,.15]:c.part('button',spine,[0,y,.21],[.035,.035,.025],'edge',1)
        c.part('backpack',spine,[0,.04,-.29],[.47,.5,.26],'dark')
        c.part('backpack_flap',spine,[0,.13,-.44],[.4,.20,.06],'stone')
        c.part('bedroll',spine,[0,.36,-.28],[.53,.18,.19],'edge',.7)
        for s in [-1,1]:c.part('roll_strap',spine,[s*.17,.36,-.28],[.045,.20,.21],'dark')
        c.part('hair',head,[0,.045,-.025],[.62,.53,.48],'dark',.8)
        c.part('face',head,[0,.015,.075],[.58,.48,.42],'skin',.9)
        for s in [-1,1]:
            c.part('ear',head,[s*.31,-.025,.035],[.13,.18,.13],'skin',1)
            c.part('eye_white',head,[s*.125,.055,.27],[.13,.17,.04],'white',1)
            c.part('pupil',head,[s*.125,.05,.295],[.073,.10,.028],'black',1)
            c.part('eye_highlight',head,[s*.125-.012,.074,.31],[.021,.024,.01],'white',1)
            c.part('eyebrow',head,[s*.125,.175,.26],[.16,.04,.04],'dark',.5)
        c.part('nose',head,[0,-.045,.31],[.16,.135,.13],'skin',1)
        c.part('smile',head,[0,-.155,.27],[.13,.021,.016],'dark',.6)
        c.part('helmet_dome',head,[0,.26,0],[.69,.38,.57],'gold',.75)
        c.part('helmet_brim',head,[0,.16,.04],[.80,.06,.69],'gold',.4)
        c.part('helmet_band',head,[0,.22,0],[.71,.065,.59],'dark',.6)
        c.part('lamp_rim',head,[0,.31,.30],[.25,.25,.10],'metal',.8)
        c.part('lamp_glass',head,[0,.31,.36],[.19,.19,.035],'lamp',1)
    for a,e,h in arms:
        c.part('upper_arm',a,[0,-.125,0],[.29 if guard else .20,.29,.28 if guard else .21],'stone' if guard else 'gold',.25 if guard else .65)
        c.part('forearm',e,[0,-.115,0],[.27 if guard else .17,.25,.25 if guard else .18],'edge' if guard else 'skin',.25 if guard else .8)
        c.part('hand',h,[0,-.06,.015],[.29 if guard else .19,.20,.25 if guard else .18],'stone' if guard else 'dark',.3)
        if guard:
            for dx in [-.07,0,.07]:c.part('finger_seam',h,[dx,-.08,.145],[.012,.08,.012],'dark',.3)
    for l,k,f in legs:
        c.part('thigh',l,[0,-.14,0],[.30 if guard else .20,.30,.28 if guard else .22],'stone' if guard else 'dark',.3)
        c.part('shin',k,[0,-.12,0],[.27 if guard else .18,.26,.26 if guard else .21],'edge' if guard else 'stone',.3)
        c.part('boot',f,[0,-.02,.04],[.34 if guard else .26,.19,.43 if guard else .36],'stone' if guard else 'dark',.3)
        c.part('sole',f,[0,-.10,.045],[.35 if guard else .27,.045,.44 if guard else .37],'edge',.25)
    tool=None
    if not guard:
        tool=c.joint('pickaxe_socket',arms[1][2],[0,-.02,.08])
        add_pickaxe(c,tool)
    tracks=[]
    idle=[(spine,'rotation',lambda t:q(.018*math.sin(t*2*math.pi)))]
    walk=[];climb=[]
    for i,((a,e,h),(l,k,f)) in enumerate(zip(arms,legs)):
        sign=(-1)**i
        walk += [(a,'rotation',lambda t,s=sign:q(-s*.48*math.sin(t*2*math.pi))),(l,'rotation',lambda t,s=sign:q(s*.45*math.sin(t*2*math.pi))),(k,'rotation',lambda t,s=sign:q(max(0,-s*math.sin(t*2*math.pi))*.45))]
        climb += [(a,'rotation',lambda t,s=sign:q(-2.3+s*.5*math.sin(t*2*math.pi))),(e,'rotation',lambda t:q(-.45)),(l,'rotation',lambda t,s=sign:q(-.5+s*.5*math.sin(t*2*math.pi)))]
    clips=[('Idle',2,idle),('Walk',.9,walk),('Climb',1.2,climb),('Fall',.6,[(a,'rotation',lambda t:q(-.6)) for a,e,h in arms])]
    if not guard:
        clips.append(('Dig',.8,[(arms[1][0],'rotation',lambda t:q(-.8-1.1*math.sin(math.pi*t)**2)),(arms[1][1],'rotation',lambda t:q(-.3-.7*math.sin(math.pi*t)**2)),(spine,'rotation',lambda t:q(.18*math.sin(math.pi*t)**2))]))
    else:clips.append(('Struggle',1,[(a,'rotation',lambda t:q(-1.8+.25*math.sin(t*4*math.pi))) for a,e,h in arms]))
    return c,clips

def add_pickaxe(c,b):
    c.part('pickaxe_handle',b,[0,.17,0],[.055,.66,.055],'stone',.6)
    c.part('pickaxe_head',b,[0,.48,0],[.46,.08,.095],'metal',.5)
    for s in [-1,1]:c.part('pickaxe_tip',b,[s*.25,.43,0],[.17,.05,.065],'metal',.65,rot=-s*.5)

if __name__=='__main__':
    models=[];stats=[]
    for g in [False,True]:
        c,clips=make(g);stats.append(c.export(clips));models.append(c)
    tool=Character('pickaxe');tool.mat('stone',(.36,.20,.09));tool.mat('metal',(.30,.35,.40),.65);b=tool.joint('root',None,[0,0,0]);add_pickaxe(tool,b);stats.append(tool.export([]))
    (ROOT/'manifest.json').write_text(json.dumps(stats,indent=2))
    # Render real mesh geometry, rather than an AI concept image.
    import matplotlib;matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection
    fig=plt.figure(figsize=(12,7),facecolor='#ece6dc')
    for ci,c in enumerate(models):
        ax=fig.add_subplot(1,2,ci+1,projection='3d',computed_zorder=False);ax.set_facecolor('#ece6dc')
        light=np.array([-.4,.8,.6]);light/=np.linalg.norm(light)
        polys=[];colors=[]
        for name,b,p,f,mi in c.parts:
            base=np.array(c.materials[mi]['pbrMetallicRoughness']['baseColorFactor'][:3])
            for tri in f:
                verts=p[tri];n=np.cross(verts[1]-verts[0],verts[2]-verts[0]);n/=max(np.linalg.norm(n),1e-9)
                col=np.clip(base*(.55+.45*max(0,np.dot(n,light))),0,1)
                polys.append(verts[:,[0,2,1]]);colors.append(col)
        ax.add_collection3d(Poly3DCollection(polys,facecolors=colors,edgecolors='none',zsort='average'))
        ax.set(xlim=(-.9,.9),ylim=(-.9,.9),zlim=(0,2.1));ax.set_box_aspect((1.8,1.8,2.1));ax.view_init(elev=12,azim=65);ax.set_axis_off();ax.set_title(c.name.upper(),fontsize=18,color='#34322e',pad=0)
    fig.suptitle('DUNGEON GUARDIANS / ACTUAL GLB GEOMETRY / PROTOTYPE 0.1',fontsize=13,color='#605447');plt.tight_layout();plt.savefig(ROOT/'preview.png',dpi=130,facecolor=fig.get_facecolor());plt.close()
    print(json.dumps(stats))
