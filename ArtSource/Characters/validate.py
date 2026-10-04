import pathlib,struct,json,numpy as np
root=pathlib.Path(__file__).parent
for path in root.glob('*.glb'):
    data=path.read_bytes(); magic,version,total=struct.unpack_from('<III',data)
    assert magic==0x46546c67 and version==2 and total==len(data)
    size,kind=struct.unpack_from('<II',data,12);assert kind==0x4e4f534a
    d=json.loads(data[20:20+size]);bs,bk=struct.unpack_from('<II',data,20+size);assert bk==0x004e4942
    binary=data[28+size:];assert bs==len(binary)
    def read(i):
        a=d['accessors'][i];v=d['bufferViews'][a['bufferView']];n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];dtype={5126:'<f4',5123:'<u2',5125:'<u4'}[a['componentType']]
        assert v['byteOffset']%4==0 and v['byteOffset']+v['byteLength']<=len(binary)
        arr=np.frombuffer(binary,dtype=dtype,count=a['count']*n,offset=v['byteOffset']).reshape(a['count'],n);assert np.isfinite(arr).all();return arr
    for i in range(len(d['accessors'])):read(i)
    skin=d['skins'][0];ib=read(skin['inverseBindMatrices'])
    parents={child:i for i,n in enumerate(d['nodes']) for child in n.get('children',[])}
    def world(i):return np.array(d['nodes'][i].get('translation',[0,0,0]))+(world(parents[i]) if i in parents else 0)
    for j,node in enumerate(skin['joints']):
        mat=ib[j].reshape(4,4).T;assert np.allclose(mat[:3,3],-world(node),atol=1e-6)
    for p in d['meshes'][0]['primitives']:
        a=p['attributes'];pos=read(a['POSITION']);ind=read(p['indices']).flatten();assert ind.max()<len(pos)
        assert read(a['JOINTS_0']).max()<len(skin['joints']);assert np.allclose(read(a['WEIGHTS_0']).sum(axis=1),1)
        assert np.allclose(np.linalg.norm(read(a['NORMAL']),axis=1),1,atol=1e-4)
        verts=pos[ind.reshape(-1,3)];areas=np.linalg.norm(np.cross(verts[:,1]-verts[:,0],verts[:,2]-verts[:,0]),axis=1);assert (areas>1e-10).all()
    for clip in d['animations']:
        for ch in clip['channels']:
            sam=clip['samplers'][ch['sampler']];t=read(sam['input']);v=read(sam['output']);assert len(t)==len(v) and (np.diff(t[:,0])>0).all()
            if ch['target']['path']=='rotation':assert np.allclose(np.linalg.norm(v,axis=1),1,atol=1e-5)
    print(path.name, 'PASS: GLB structure, geometry, normals, rigid skin, bind transforms, animation channels')
