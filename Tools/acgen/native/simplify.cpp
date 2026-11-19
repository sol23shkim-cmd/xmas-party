// 이차 오차(QEM) 메시 단순화 — sp4cerat의 Fast-Quadric-Mesh-Simplification 방식을 바탕으로 색 경계 보존 벌점을 더함.
// 사용: simplify in.bin out.bin target_tris color_weight
// in.bin: int32 nv, int32 nf, float32[nv*3] pos, float32[nv*3] col, int32[nf*3] faces
// out.bin: int32 nv, int32 nf, float32[nv*3] pos, int32[nf*3] faces, int32[nv] origin(가장 가까운 원래 정점)
#include <cstdio>
#include <cstring>
#include <cstdlib>
#include <cmath>
#include <vector>
#include <algorithm>
using namespace std;
struct V3 { double x, y, z; V3(){x=y=z=0;} V3(double a,double b,double c):x(a),y(b),z(c){}
  V3 operator+(const V3&o)const{return V3(x+o.x,y+o.y,z+o.z);} V3 operator-(const V3&o)const{return V3(x-o.x,y-o.y,z-o.z);}
  V3 operator*(double s)const{return V3(x*s,y*s,z*s);} double dot(const V3&o)const{return x*o.x+y*o.y+z*o.z;}
  V3 cross(const V3&o)const{return V3(y*o.z-z*o.y,z*o.x-x*o.z,x*o.y-y*o.x);} V3 norm()const{double l=sqrt(dot(*this));return l>0?(*this)*(1/l):*this;} };
struct SM { double m[10]; SM(double c=0){for(int i=0;i<10;i++)m[i]=c;}
  SM(double a,double b,double c,double d){m[0]=a*a;m[1]=a*b;m[2]=a*c;m[3]=a*d;m[4]=b*b;m[5]=b*c;m[6]=b*d;m[7]=c*c;m[8]=c*d;m[9]=d*d;}
  double operator[](int i)const{return m[i];}
  double det(int a11,int a12,int a13,int a21,int a22,int a23,int a31,int a32,int a33)const{
    return m[a11]*m[a22]*m[a33]+m[a13]*m[a21]*m[a32]+m[a12]*m[a23]*m[a31]-m[a13]*m[a22]*m[a31]-m[a11]*m[a23]*m[a32]-m[a12]*m[a21]*m[a33];}
  SM operator+(const SM&n)const{SM r;for(int i=0;i<10;i++)r.m[i]=m[i]+n.m[i];return r;} };
struct Tri { int v[3]; double err[4]; bool deleted, dirty; V3 n; };
struct Vert { V3 p; V3 c; int tstart, tcount; SM q; bool border; int origin; };
struct Ref { int tid, tvertex; };
vector<Tri> T; vector<Vert> Vs; vector<Ref> refs; double CW = 0;

double vertex_error(const SM&q,double x,double y,double z){
  return q[0]*x*x+2*q[1]*x*y+2*q[2]*x*z+2*q[3]*x+q[4]*y*y+2*q[5]*y*z+2*q[6]*y+q[7]*z*z+2*q[8]*z+q[9]; }
double calculate_error(int a,int b,V3&res){
  SM q=Vs[a].q+Vs[b].q; bool border=Vs[a].border&&Vs[b].border; double err=0; double det=q.det(0,1,2,1,4,5,2,5,7);
  if(det!=0&&!border){ res.x=-1/det*q.det(1,2,3,4,5,6,5,7,8); res.y=1/det*q.det(0,2,3,1,5,6,2,7,8); res.z=-1/det*q.det(0,1,3,1,4,6,2,5,8);
    // 너무 멀리 튀면 중점으로
    V3 mid=(Vs[a].p+Vs[b].p)*0.5; V3 d=res-mid; double L=(Vs[a].p-Vs[b].p).dot(Vs[a].p-Vs[b].p);
    if(d.dot(d)>4*L+1e-12) res=mid;
    err=vertex_error(q,res.x,res.y,res.z); }
  else { V3 p1=Vs[a].p,p2=Vs[b].p,p3=(p1+p2)*0.5; double e1=vertex_error(q,p1.x,p1.y,p1.z),e2=vertex_error(q,p2.x,p2.y,p2.z),e3=vertex_error(q,p3.x,p3.y,p3.z);
    err=min(e1,min(e2,e3)); if(err==e1)res=p1; if(err==e2)res=p2; if(err==e3)res=p3; }
  V3 dc=Vs[a].c-Vs[b].c; V3 dl=Vs[a].p-Vs[b].p; err+=CW*dc.dot(dc)*dl.dot(dl);
  return err; }
bool flipped(V3 p,int i0,int i1,Vert&v0,vector<int>&deleted){
  for(int k=0;k<v0.tcount;k++){ Tri&t=T[refs[v0.tstart+k].tid]; if(t.deleted)continue; int s=refs[v0.tstart+k].tvertex;
    int id1=t.v[(s+1)%3],id2=t.v[(s+2)%3]; if(id1==i1||id2==i1){deleted[k]=1;continue;}
    V3 d1=(Vs[id1].p-p).norm(), d2=(Vs[id2].p-p).norm(); if(fabs(d1.dot(d2))>0.999)return true;
    V3 n=d1.cross(d2).norm(); deleted[k]=0; if(n.dot(t.n)<0.2)return true; }
  return false; }
void update_triangles(int i0,Vert&v,vector<int>&deleted,int&dc){
  V3 p; for(int k=0;k<v.tcount;k++){ Ref&r=refs[v.tstart+k]; Tri&t=T[r.tid]; if(t.deleted)continue;
    if(deleted[k]){t.deleted=1;dc++;continue;} t.v[r.tvertex]=i0; t.dirty=1;
    t.err[0]=calculate_error(t.v[0],t.v[1],p); t.err[1]=calculate_error(t.v[1],t.v[2],p); t.err[2]=calculate_error(t.v[2],t.v[0],p);
    t.err[3]=min(t.err[0],min(t.err[1],t.err[2])); refs.push_back(r);} }
void update_mesh(int iteration){
  if(iteration>0){int dst=0; for(size_t i=0;i<T.size();i++) if(!T[i].deleted) T[dst++]=T[i]; T.resize(dst);}
  for(auto&v:Vs){v.tstart=0;v.tcount=0;}
  for(auto&t:T) for(int j=0;j<3;j++) Vs[t.v[j]].tcount++;
  int ts=0; for(auto&v:Vs){v.tstart=ts;ts+=v.tcount;v.tcount=0;}
  refs.resize(T.size()*3);
  for(size_t i=0;i<T.size();i++) for(int j=0;j<3;j++){Vert&v=Vs[T[i].v[j]]; refs[v.tstart+v.tcount].tid=i; refs[v.tstart+v.tcount].tvertex=j; v.tcount++;}
  if(iteration==0){
    vector<int> vcount,vids;
    for(auto&v:Vs) v.border=0;
    for(auto&v:Vs){ vcount.clear(); vids.clear();
      for(int j=0;j<v.tcount;j++){ Tri&t=T[refs[v.tstart+j].tid];
        for(int k=0;k<3;k++){ size_t ofs=0; int id=t.v[k]; while(ofs<vcount.size()){if(vids[ofs]==id)break;ofs++;}
          if(ofs==vcount.size()){vcount.push_back(1);vids.push_back(id);} else vcount[ofs]++; } }
      for(size_t j=0;j<vcount.size();j++) if(vcount[j]==1) Vs[vids[j]].border=1; }
    for(auto&v:Vs) v.q=SM(0.0);
    for(auto&t:T){ V3 p[3]; for(int j=0;j<3;j++)p[j]=Vs[t.v[j]].p; V3 n=(p[1]-p[0]).cross(p[2]-p[0]).norm(); t.n=n;
      for(int j=0;j<3;j++) Vs[t.v[j]].q=Vs[t.v[j]].q+SM(n.x,n.y,n.z,-n.dot(p[0])); }
    for(auto&t:T){ V3 p; for(int j=0;j<3;j++) t.err[j]=calculate_error(t.v[j],t.v[(j+1)%3],p); t.err[3]=min(t.err[0],min(t.err[1],t.err[2])); }
  } }
void compact(){
  int dst=0; for(size_t i=0;i<T.size();i++) if(!T[i].deleted) T[dst++]=T[i]; T.resize(dst);
  for(auto&v:Vs) v.tcount=0; for(auto&t:T) for(int j=0;j<3;j++) Vs[t.v[j]].tcount=1;
  dst=0; for(size_t i=0;i<Vs.size();i++) if(Vs[i].tcount){Vs[i].tstart=dst; Vs[dst].p=Vs[i].p; Vs[dst].origin=Vs[i].origin; dst++;}
  for(auto&t:T) for(int j=0;j<3;j++) t.v[j]=Vs[t.v[j]].tstart; Vs.resize(dst); }
int main(int argc,char**argv){
  if(argc<5){fprintf(stderr,"usage\n");return 1;}
  FILE*f=fopen(argv[1],"rb"); int nv,nf; fread(&nv,4,1,f); fread(&nf,4,1,f);
  vector<float> P(nv*3),C(nv*3); vector<int> F(nf*3); fread(P.data(),4,nv*3,f); fread(C.data(),4,nv*3,f); fread(F.data(),4,nf*3,f); fclose(f);
  int target=atoi(argv[3]); CW=atof(argv[4]);
  Vs.resize(nv); for(int i=0;i<nv;i++){Vs[i].p=V3(P[i*3],P[i*3+1],P[i*3+2]); Vs[i].c=V3(C[i*3],C[i*3+1],C[i*3+2]); Vs[i].origin=i;}
  T.resize(nf); for(int i=0;i<nf;i++){for(int j=0;j<3;j++)T[i].v[j]=F[i*3+j]; T[i].deleted=0; T[i].dirty=0;}
  int deleted_triangles=0; vector<int> del0,del1; int tri_count=T.size();
  for(int iteration=0;iteration<120;iteration++){
    if(tri_count-deleted_triangles<=target)break;
    if(iteration%5==0) update_mesh(iteration);
    for(auto&t:T) t.dirty=0;
    double threshold=1e-9*pow(double(iteration+3),7.0);
    for(size_t i=0;i<T.size();i++){ Tri&t=T[i]; if(t.err[3]>threshold||t.deleted||t.dirty)continue;
      for(int j=0;j<3;j++) if(t.err[j]<threshold){
        int i0=t.v[j]; Vert&v0=Vs[i0]; int i1=t.v[(j+1)%3]; Vert&v1=Vs[i1];
        if(v0.border!=v1.border)continue;
        V3 p; calculate_error(i0,i1,p);
        del0.resize(v0.tcount); del1.resize(v1.tcount);
        if(flipped(p,i0,i1,v0,del0))continue; if(flipped(p,i1,i0,v1,del1))continue;
        v0.p=p; v0.q=v1.q+v0.q;
        int tstart=refs.size();
        update_triangles(i0,v0,del0,deleted_triangles); update_triangles(i0,v1,del1,deleted_triangles);
        int tcount=refs.size()-tstart;
        if(tcount<=v0.tcount){ if(tcount) memmove(&refs[v0.tstart],&refs[tstart],tcount*sizeof(Ref)); }
        else v0.tstart=tstart;
        v0.tcount=tcount; break; }
      if(tri_count-deleted_triangles<=target)break; } }
  compact();
  f=fopen(argv[2],"wb"); nv=Vs.size(); nf=T.size(); fwrite(&nv,4,1,f); fwrite(&nf,4,1,f);
  for(auto&v:Vs){float a[3]={(float)v.p.x,(float)v.p.y,(float)v.p.z}; fwrite(a,4,3,f);}
  for(auto&t:T) fwrite(t.v,4,3,f);
  for(auto&v:Vs) fwrite(&v.origin,4,1,f);
  fclose(f); return 0; }
