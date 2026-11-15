#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NightShift.EditorBuild
{
    public class Geo
    {
        class Part
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
        }

        readonly Dictionary<string,Part> _parts = new Dictionary<string,Part>();
        readonly List<string> _order = new List<string>();
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();

        public Matrix4x4 M = Matrix4x4.identity;
        
        public float UvScale = 1f;

        public bool IsEmpty => _order.Count == 0;

        public int VertexCount
        {
            get { int c = 0; foreach (var k in _order) c += _parts[k].v.Count; return c; }
        }

        Part P(string mat)
        {
            if (!_parts.TryGetValue(mat,out var p)) { p = new Part(); _parts[mat] = p; _order.Add(mat); }
            return p;
        }

sealed class Popper : IDisposable
        {
            readonly Geo _g;
            public Popper(Geo g) { _g = g; }
            public void Dispose() { _g.M = _g._stack.Pop(); }
        }

        public IDisposable Push(Matrix4x4 local) { _stack.Push(M); M = M * local; return new Popper(this); }
        public IDisposable At(Vector3 pos,float yawDeg = 0f) => Push(Matrix4x4.TRS(pos,Quaternion.Euler(0f,yawDeg,0f),Vector3.one));
        public IDisposable At(Vector3 pos,Quaternion rot) => Push(Matrix4x4.TRS(pos,rot,Vector3.one));
        public IDisposable At(Vector3 pos,Quaternion rot,Vector3 scale) => Push(Matrix4x4.TRS(pos,rot,scale));

static Vector2 BoxUV(Vector3 p,Vector3 n)
        {
            float ax = Mathf.Abs(n.x),ay = Mathf.Abs(n.y),az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az) return new Vector2(p.x,p.z);
            if (ax >= az) return new Vector2(p.z,p.y);
            return new Vector2(p.x,p.y);
        }

        int V(Part p,Vector3 pos,Vector3 nrm)
        {
            Vector3 wp = M.MultiplyPoint3x4(pos);
            Vector3 wn = M.MultiplyVector(nrm).normalized;
            p.v.Add(wp); p.n.Add(wn); p.uv.Add(BoxUV(wp,wn) * UvScale);
            return p.v.Count - 1;
        }

        int V(Part p,Vector3 pos,Vector3 nrm,Vector2 uv)
        {
            p.v.Add(M.MultiplyPoint3x4(pos)); p.n.Add(M.MultiplyVector(nrm).normalized); p.uv.Add(uv);
            return p.v.Count - 1;
        }

        
        void Tri(Part p,int a,int b,int c)
        {
            Vector3 g = Vector3.Cross(p.v[b] - p.v[a],p.v[c] - p.v[a]);
            if (g.sqrMagnitude < 1e-14f) return;
            if (Vector3.Dot(g,p.n[a] + p.n[b] + p.n[c]) < 0f) { int tmp = b; b = c; c = tmp; }
            p.t.Add(a); p.t.Add(b); p.t.Add(c);
        }

        void Quad4(Part p,int a,int b,int c,int d) { Tri(p,a,b,c); Tri(p,a,c,d); }

public void Box(string mat,Vector3 c,Vector3 size,float bevel = 0.006f)
        {
            var p = P(mat);
            Vector3 h = size * 0.5f;
            float b = Mathf.Min(bevel,Mathf.Min(h.x,Mathf.Min(h.y,h.z)) * 0.45f);
            if (b < 0.0004f) b = 0f;
            var idx = new int[8,3];
            for (int corner = 0; corner < 8; corner++)
            {
                float sx = (corner & 1) != 0 ? 1f : -1f,sy = (corner & 2) != 0 ? 1f : -1f,sz = (corner & 4) != 0 ? 1f : -1f;
                for (int a = 0; a < 3; a++)
                {
                    Vector3 pos = new Vector3(sx * (h.x - (a == 0 ? 0f : b)),sy * (h.y - (a == 1 ? 0f : b)),sz * (h.z - (a == 2 ? 0f : b)));
                    Vector3 nrm = a == 0 ? new Vector3(sx,0f,0f) : a == 1 ? new Vector3(0f,sy,0f) : new Vector3(0f,0f,sz);
                    idx[corner,a] = V(p,c + pos,nrm);
                }
            }
            
            for (int a = 0; a < 3; a++)
            {
                int bitA = 1 << a,bitU = 1 << ((a + 1) % 3),bitV = 1 << ((a + 2) % 3);
                for (int s = 0; s < 2; s++)
                {
                    int baseC = s == 1 ? bitA : 0;
                    Quad4(p,idx[baseC,a],idx[baseC | bitU,a],idx[baseC | bitU | bitV,a],idx[baseC | bitV,a]);
                }
            }
            if (b <= 0f) return;
            
            for (int d = 0; d < 3; d++)
            {
                int bitD = 1 << d,a1 = (d + 1) % 3,a2 = (d + 2) % 3,bit1 = 1 << a1,bit2 = 1 << a2;
                for (int s1 = 0; s1 < 2; s1++)
                    for (int s2 = 0; s2 < 2; s2++)
                    {
                        int lo = (s1 == 1 ? bit1 : 0) | (s2 == 1 ? bit2 : 0),hi = lo | bitD;
                        Quad4(p,idx[lo,a1],idx[hi,a1],idx[hi,a2],idx[lo,a2]);
                    }
            }
            
            for (int corner = 0; corner < 8; corner++) Tri(p,idx[corner,0],idx[corner,1],idx[corner,2]);
        }

        
        public void Tube(string mat,Vector3 a,Vector3 b,float r0,float r1,int seg = 12,bool capA = true,bool capB = true)
        {
            var p = P(mat);
            Vector3 axis = b - a;
            float len = axis.magnitude;
            if (len < 1e-5f) return;
            Vector3 w = axis / len;
            Vector3 u = Vector3.Cross(w,Mathf.Abs(w.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(w,u);
            int[] ra = new int[seg + 1],rb = new int[seg + 1];
            float circ = 2f * Mathf.PI * Mathf.Max(r0,r1);
            for (int i = 0; i <= seg; i++)
            {
                float ang = i / (float)seg * Mathf.PI * 2f;
                Vector3 radial = Mathf.Cos(ang) * u + Mathf.Sin(ang) * v;
                Vector3 nrm = (radial * len + w * (r0 - r1)).normalized;
                float uu = i / (float)seg * circ * UvScale;
                ra[i] = V(p,a + radial * r0,nrm,new Vector2(uu,0f));
                rb[i] = V(p,b + radial * r1,nrm,new Vector2(uu,len * UvScale));
            }
            for (int i = 0; i < seg; i++) Quad4(p,ra[i],rb[i],rb[i + 1],ra[i + 1]);
            if (capA && r0 > 1e-4f) Cap(p,a,-w,u,v,r0,seg);
            if (capB && r1 > 1e-4f) Cap(p,b,w,u,v,r1,seg);
        }

        void Cap(Part p,Vector3 c,Vector3 nrm,Vector3 u,Vector3 v,float r,int seg)
        {
            int ci = V(p,c,nrm);
            int prev = -1,first = -1;
            for (int i = 0; i < seg; i++)
            {
                float ang = i / (float)seg * Mathf.PI * 2f;
                int vi = V(p,c + (Mathf.Cos(ang) * u + Mathf.Sin(ang) * v) * r,nrm);
                if (i == 0) first = vi; else Tri(p,ci,prev,vi);
                prev = vi;
            }
            Tri(p,ci,prev,first);
        }

        
        public void Cyl(string mat,Vector3 baseCenter,float r,float h,int seg = 12,float rTop = -1f)
            => Tube(mat,baseCenter,baseCenter + Vector3.up * h,r,rTop < 0f ? r : rTop,seg);

        
        public void Lathe(string mat,Vector3 baseCenter,Vector2[] prof,int seg = 14)
        {
            var p = P(mat);
            int rings = prof.Length;
            var idx = new int[rings,seg + 1];
            for (int k = 0; k < rings; k++)
            {
                Vector2 tan = prof[Mathf.Min(k + 1,rings - 1)] - prof[Mathf.Max(k - 1,0)];
                Vector2 n2 = new Vector2(tan.y,-tan.x).normalized;
                if (n2.sqrMagnitude < 0.5f) n2 = Vector2.right;
                for (int i = 0; i <= seg; i++)
                {
                    float ang = i / (float)seg * Mathf.PI * 2f;
                    Vector3 radial = new Vector3(Mathf.Cos(ang),0f,Mathf.Sin(ang));
                    Vector3 pos = baseCenter + radial * prof[k].x + Vector3.up * prof[k].y;
                    Vector3 nrm = radial * n2.x + Vector3.up * n2.y;
                    idx[k,i] = V(p,pos,nrm,new Vector2(i / (float)seg * 2f * Mathf.PI * Mathf.Max(0.02f,prof[k].x),prof[k].y) * UvScale);
                }
            }
            for (int k = 0; k < rings - 1; k++)
                for (int i = 0; i < seg; i++)
                    Quad4(p,idx[k,i],idx[k + 1,i],idx[k + 1,i + 1],idx[k,i + 1]);
        }

public void Loft(string mat,Vector3 origin,Vector4[] sections,int seg = 14,bool capStart = true,bool capEnd = true)
        {
            var p = P(mat);
            int rings = sections.Length;
            var pos = new Vector3[rings,seg + 1];
            for (int k = 0; k < rings; k++)
            {
                var s = sections[k];
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    pos[k,i] = origin + new Vector3(Mathf.Cos(a) * s.y,s.x,Mathf.Sin(a) * s.z + s.w);
                }
            }
            var idx = new int[rings,seg + 1];
            float dir = sections[rings - 1].x >= sections[0].x ? 1f : -1f;
            for (int k = 0; k < rings; k++)
            {
                for (int i = 0; i <= seg; i++)
                {
                    Vector3 along = pos[Mathf.Min(k + 1,rings - 1),i] - pos[Mathf.Max(k - 1,0),i];
                    Vector3 around = pos[k,(i + 1) % seg] - pos[k,(i - 1 + seg) % seg];
                    Vector3 n = Vector3.Cross(around,along * dir);
                    Vector3 centre = origin + new Vector3(0f,sections[k].x,sections[k].w);
                    Vector3 radial = pos[k,i] - centre;
                    if (n.sqrMagnitude < 1e-10f) n = radial;
                    if (Vector3.Dot(n,radial) < 0f) n = -n;
                    idx[k,i] = V(p,pos[k,i],n.normalized,new Vector2(i / (float)seg,k / (float)(rings - 1)));
                }
            }
            for (int k = 0; k < rings - 1; k++)
                for (int i = 0; i < seg; i++)
                    Quad4(p,idx[k,i],idx[k + 1,i],idx[k + 1,i + 1],idx[k,i + 1]);
            if (capStart && sections[0].y > 1e-4f) LoftCap(p,origin,sections[0],seg,-dir);
            if (capEnd && sections[rings - 1].y > 1e-4f) LoftCap(p,origin,sections[rings - 1],seg,dir);
        }

        void LoftCap(Part p,Vector3 origin,Vector4 s,int seg,float sign)
        {
            Vector3 nrm = Vector3.up * sign;
            int c = V(p,origin + new Vector3(0f,s.x,s.w),nrm);
            int first = -1,prev = -1;
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                int vi = V(p,origin + new Vector3(Mathf.Cos(a) * s.y,s.x,Mathf.Sin(a) * s.z + s.w),nrm);
                if (i == 0) first = vi; else Tri(p,c,prev,vi);
                prev = vi;
            }
            Tri(p,c,prev,first);
        }

        
        public void Ball(string mat,Vector3 c,Vector3 radii,int seg = 12,int rings = 8)
        {
            var p = P(mat);
            var idx = new int[rings + 1,seg + 1];
            for (int k = 0; k <= rings; k++)
            {
                float phi = k / (float)rings * Mathf.PI;
                for (int i = 0; i <= seg; i++)
                {
                    float th = i / (float)seg * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th),Mathf.Cos(phi),Mathf.Sin(phi) * Mathf.Sin(th));
                    Vector3 nrm = new Vector3(d.x / radii.x,d.y / radii.y,d.z / radii.z).normalized;
                    idx[k,i] = V(p,c + Vector3.Scale(d,radii),nrm,new Vector2(i / (float)seg,1f - k / (float)rings));
                }
            }
            for (int k = 0; k < rings; k++)
                for (int i = 0; i < seg; i++)
                    Quad4(p,idx[k,i],idx[k + 1,i],idx[k + 1,i + 1],idx[k,i + 1]);
        }

public void Quad(string mat,Vector3 c,Vector3 halfRight,Vector3 halfUp,bool twoSided = false,bool tiled = false)
        {
            var p = P(mat);
            Vector3 nrm = Vector3.Cross(halfRight,halfUp).normalized;
            int a,b,cc,d;
            if (tiled)
            {
                a = V(p,c - halfRight - halfUp,nrm); b = V(p,c - halfRight + halfUp,nrm);
                cc = V(p,c + halfRight + halfUp,nrm); d = V(p,c + halfRight - halfUp,nrm);
            }
            else
            {
                a = V(p,c - halfRight - halfUp,nrm,new Vector2(1f,0f)); b = V(p,c - halfRight + halfUp,nrm,new Vector2(1f,1f));
                cc = V(p,c + halfRight + halfUp,nrm,new Vector2(0f,1f)); d = V(p,c + halfRight - halfUp,nrm,new Vector2(0f,0f));
            }
            Quad4(p,a,b,cc,d);
            if (twoSided)
            {
                int a2 = V(p,c - halfRight - halfUp,-nrm,new Vector2(1f,0f)),b2 = V(p,c - halfRight + halfUp,-nrm,new Vector2(1f,1f));
                int c2 = V(p,c + halfRight + halfUp,-nrm,new Vector2(0f,1f)),d2 = V(p,c + halfRight - halfUp,-nrm,new Vector2(0f,0f));
                Quad4(p,a2,b2,c2,d2);
            }
        }

        
        public void Blade(string mat,Vector3 root,Vector3 tip,Vector3 bendDir,float width,int segs = 3)
        {
            var p = P(mat);
            Vector3 along = tip - root;
            Vector3 side = Vector3.Cross(along.normalized,bendDir.sqrMagnitude > 0.001f ? bendDir.normalized : Vector3.up);
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            side.Normalize();
            for (int pass = 0; pass < 2; pass++)
            {
                int prevL = -1,prevR = -1;
                for (int i = 0; i <= segs; i++)
                {
                    float k = i / (float)segs;
                    Vector3 c = root + along * k + bendDir * (k * k);
                    float w = width * Mathf.Sin(Mathf.Lerp(0.35f,1f,k) * Mathf.PI) * (1f - k * 0.55f) + 0.004f;
                    Vector3 nrm = Vector3.Cross(side,(along + bendDir * (2f * k)).normalized).normalized * (pass == 0 ? 1f : -1f);
                    int l = V(p,c - side * w,nrm,new Vector2(0f,k)),r = V(p,c + side * w,nrm,new Vector2(1f,k));
                    if (i > 0) Quad4(p,prevL,l,r,prevR);
                    prevL = l; prevR = r;
                }
            }
        }

public Mesh BuildMesh(string name,out string[] mats)
        {
            var mesh = new Mesh { name = name };
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var keys = new List<string>();
            var tris = new List<List<int>>();
            foreach (var k in _order)
            {
                var p = _parts[k];
                if (p.t.Count == 0) continue;
                int off = verts.Count;
                verts.AddRange(p.v); norms.AddRange(p.n); uvs.AddRange(p.uv);
                var tl = new List<int>(p.t.Count);
                for (int i = 0; i < p.t.Count; i++) tl.Add(p.t[i] + off);
                tris.Add(tl); keys.Add(k);
            }
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0,uvs);
            mesh.subMeshCount = keys.Count;
            for (int i = 0; i < keys.Count; i++) mesh.SetTriangles(tris[i],i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mats = keys.ToArray();
            return mesh;
        }

        
        public GameObject Emit(string name,Transform parent,bool isStatic,bool shadows = true)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent,false);
            if (IsEmpty) return go;
            var mesh = BuildMesh(name,out var keys);
            MeshStore.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mats = new Material[keys.Length];
            for (int i = 0; i < keys.Length; i++) mats[i] = Mats.Get(keys[i]);
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (isStatic)
                GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }
    }

    
    public static class MeshStore
    {
        const string Path = "Assets/Generated/OfficeMeshes.asset";
        static bool _created;
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();
        static readonly HashSet<string> Names = new HashSet<string>();

        public static void Begin()
        {
            System.IO.Directory.CreateDirectory("Assets/Generated");
            AssetDatabase.DeleteAsset(Path);
            _created = false;
            Cache.Clear(); Names.Clear();
        }

        public static void Add(Mesh mesh)
        {
            string n = mesh.name; int i = 1;
            while (!Names.Add(n)) n = mesh.name + "_" + (i++);
            mesh.name=n;
            if (!_created) { AssetDatabase.CreateAsset(mesh, Path); _created=true; }
            else AssetDatabase.AddObjectToAsset(mesh, Path);
        }

        
        public static Mesh Shared(string key, Func<Geo> build, out string[] mats)
        {
            if (Cache.TryGetValue(key, out var m)) { mats=MatKeys[key]; return m; }
            var g=build();
            m=g.BuildMesh(key, out mats);
            Add(m);
            Cache[key]=m; MatKeys[key]=mats;
            return m;
        }

        static readonly Dictionary<string, string[]> MatKeys=new Dictionary<string, string[]>();

        public static void End() { AssetDatabase.SaveAssets(); }
    }
}
#endif
