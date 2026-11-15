// L1 concept geometry, metres. NW origin; drawing y south = Unity -Z.
// Run: node ArtSource/Kit/concepts/Layout/draw_L1.mjs
// Then: & ArtSource/Kit/concepts/Layout/render_L1.ps1
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const ROOT=path.dirname(fileURLToPath(import.meta.url));
const C={ink:'#111B38',blue:'#244FB9',electric:'#2879EF',cyan:'#20D4E6',white:'#F3F4F4',line:'#C9CED3',glass:'#65A6B9',frost:'#A7CDD5',core:'#DCE2EC',void:'#E6F1F8',low:'#8392B0',green:'#167D77',purple:'#725E9D',muted:'#526078'};
const PPM=40, OX=140, OY=290, WIDTH=2680, HEIGHT=1880;
const xy=([x,y])=>[OX+x*PPM,OY+y*PPM];
const len=ps=>ps.slice(1).reduce((s,p,i)=>s+Math.hypot(p[0]-ps[i][0],p[1]-ps[i][1]),0);
const within=(p,r,inset=0)=>p[0]>r[0]+inset && p[0]<r[0]+r[2]-inset && p[1]>r[1]+inset && p[1]<r[1]+r[3]-inset;
const base=(id,title,w,h)=>({id,title,w,h,rooms:[],services:[],coreBands:[],walls:[],doors:[],covers:[],benches:[],labels:[],districts:[],circulation:[],circulationHoles:[],lanes:[],loops:[],nodes:[],landmarks:[],carrier:[],baffles:[]});
function wall(m,a,b,type='solid',thickness=type==='solid'?0.2:0.1){m.walls.push({a,b,type,thickness});}
function room(m,name,r,{sides=['solid','glass','frost','solid'],exits=[],service=false,kind='room',label=true}={}){
  const [x,y,w,h]=r, edges=[[[x,y],[x+w,y]],[[x+w,y],[x+w,y+h]],[[x,y+h],[x+w,y+h]],[[x,y],[x,y+h]]];
  const item={name,r,exits,kind}; (service?m.services:m.rooms).push(item);
  for(let side=0;side<4;side++){
    const [a,b]=edges[side]; if(sides[side]==='none')continue;
    const horizontal=a[1]===b[1], start=horizontal?a[0]:a[1], end=horizontal?b[0]:b[1];
    const openings=exits.filter(e=>e.side===side).sort((a,b)=>a.at-b.at); let previous=start;
    const thickness=sides[side]==='solid'?.2:.1;
    const shift=[[0,thickness/2],[-thickness/2,0],[0,-thickness/2],[thickness/2,0]][side];
    const add=(a,b)=>wall(m,a.map((v,i)=>v+shift[i]),b.map((v,i)=>v+shift[i]),sides[side],thickness);
    for(const e of openings){const size=e.width??1.2,lo=e.at-size/2,hi=e.at+size/2;
      add(horizontal?[previous,a[1]]:[a[0],previous],horizontal?[lo,a[1]]:[a[0],lo]);
      m.doors.push({p:horizontal?[e.at,a[1]]:[a[0],e.at],side,width:size,name,kind:e.kind??'door'}); previous=hi;
    }
    add(horizontal?[previous,a[1]]:[a[0],previous],b);
  }
  if(label)m.labels.push({p:[x+w/2,y+1.4],text:name,size:19}); return item;
}
function service(m,name,r,kind='sealed',exits=[]){return room(m,name,r,{sides:['solid','solid','solid','solid'],exits,service:true,kind,label:false});}
function low(m,x,y,w=2,h=1,kind='counter'){m.covers.push({r:[x-w/2,y-h/2,w,h],height:kind==='overhead'?4.3:1.1,kind});}
function bench(m,x,y){m.benches.push({r:[x-1.2,y-.8,2.4,1.6],height:1.15});}
function baffle(m,a,b){wall(m,a,b);m.baffles.push({a,b});}
function setupA(){
 const m=base('L1_A','CENTRAL CORE / RING',40,32);m.tagline='One fixed island; one dependable ring; diagonally offset cross-cut.';
 m.coreBands=[[10,8,20,16]];m.atrium=[16,10,8,12];m.feature={x:19,y:12.4,w:2,run:7.2,foot:[20,19.6],top:[20,12.4]};
 service(m,'E1', [10,8,6,8],'stair',[{side:3,at:10},{side:0,at:14}]);
 service(m,'E2', [24,16,6,8],'stair',[{side:1,at:22},{side:2,at:26}]);
 service(m,'LIFT',[24,8,4,4],'lift',[{side:0,at:26,width:1.4,kind:'lift'}]);
 service(m,'PLANT',[28,8,2,4]);service(m,'WC / RISER',[10,20,6,4]);service(m,'RISER',[10,16,2,2]);
 room(m,'C / CLAIMS\nMEETING',[0,20,8,8],{sides:['solid','solid','glass','none'],exits:[{side:0,at:4},{side:1,at:24}]});
 room(m,'A / ARCHIVE\nQUIET ROOM',[32,4,8,8],{sides:['glass','none','frost','solid'],exits:[{side:3,at:8},{side:2,at:36}]});
 m.districts=[{p:[4,7],letter:'L',name:'LEGAL / work',color:C.blue},{p:[36,25],letter:'R',name:'REVIEW / work',color:C.electric},{p:[4,30],letter:'C',name:'CLAIMS',color:C.purple},{p:[36,2],letter:'A',name:'ARCHIVE',color:C.green},{p:[18,9],letter:'H',name:'HEARINGS',color:C.blue}];
 for(const [x,y] of [[3,10],[6.4,10],[3,14],[34,18],[37,18],[34,22],[15,3],[25,29]])bench(m,x,y);
 low(m,4,24);low(m,36,8);low(m,17.5,14,1,2);low(m,22.6,18,1,2);low(m,27.8,5,3.6,1);
 low(m,17.5,20.5,2,1,'overhead'); // solid under-gallery canopy footprint, separate from low cover
 m.labels.push({p:[28,3],text:'RECEPTION',size:17},{p:[5,18],text:'PRINT / COFFEE',size:16},{p:[20,21.25],text:'LOWER LANDING',size:15});
 baffle(m,[7.9,0],[7.9,5.4]);baffle(m,[7.9,6.6],[7.9,8]);m.doors.push({p:[8,6],side:1,width:1.2,name:'Legal work nook',kind:'door'});
 baffle(m,[32.1,24],[32.1,25.4]);baffle(m,[32.1,26.6],[32.1,32]);m.doors.push({p:[32,26],side:3,width:1.2,name:'Review work nook',kind:'door'});
 baffle(m,[2,16],[4,16]);baffle(m,[36,16],[38,16]);
 m.circulation=[[8,6,24,2],[8,24,24,2],[8,8,2,16],[30,8,2,16],[10,18,6,2],[24,12,6,4]];m.circulationHoles=[];
 m.spawnA=[4,17.5];m.spawnB=[36,14.5];m.case=[20,21];
 m.loops=[[[9,7],[31,7],[31,25],[9,25],[9,7]]];
 m.lanes=[{name:'1 / north work edge',color:C.electric,p:[[4,17.5],[9,17.5],[9,7],[31,7],[31,14.5],[36,14.5]]},{name:'2 / contested mid',color:C.purple,p:[[4,17.5],[7,17.5],[7,19],[17,19],[17,21],[23,21],[23,14],[31,14],[36,14.5]]},{name:'3 / claims cut-through',color:C.green,p:[[4,17.5],[4,20],[4,21.5],[6.5,21.5],[6.5,24],[9,24],[9,25],[31,25],[31,14.5],[36,14.5]]}];
 m.nodes=[{p:[26,7],id:'N1',name:'Lift / reception'},{p:[20,21],id:'N2',name:'Case / stair'},{p:[6,18],id:'N3',name:'Print / coffee'}];
 m.landmarks=[{p:[28.5,8],id:'K1',name:'AONAGI lightbox'},{p:[18,10],id:'K2',name:'H / feature stair'},{p:[0,18],id:'K3',name:'L capsule niche'}];
 m.carrier=[{name:'F / exposed feature',color:C.blue,p:[[20,21],[20,19.6]],up:'F'},{name:'E2 / enclosed flank',color:C.green,p:[[20,21],[20,23],[20,25],[26,25],[26,24]],up:'E2'},{name:'E1 / long enclosed flank',color:C.green,p:[[20,21],[17,21],[17,19],[13,19],[9,19],[9,10],[10,10]],up:'E1'},{name:'L / loud lift proposal',color:C.purple,p:[[20,21],[23,21],[23,14],[31,14],[31,7],[26,7],[26,8]],up:'LIFT'}];
 return m;
}
function setupB(){
 const m=base('L1_B','EDGE CORE / DAYLIGHT COURT',40,28);m.tagline='A single service address; daylight wings; a compact court loop.';
 m.coreBands=[[0,0,40,8]];m.atrium=[16,8,8,12];m.feature={x:19,y:10.4,w:2,run:7.2,foot:[20,17.6],top:[20,10.4]};
 service(m,'E1',[4,0,6,8],'stair',[{side:1,at:6},{side:2,at:6}]);service(m,'E2',[30,0,6,8],'stair',[{side:3,at:6},{side:2,at:34}]);
 service(m,'LIFT',[18,0,4,4],'lift',[{side:2,at:20,width:1.4,kind:'lift'}]);service(m,'WC / RISER',[10,0,8,4]);service(m,'PLANT / STORE',[22,0,8,4]);service(m,'RISERS',[0,0,4,8]);service(m,'RISERS',[36,0,4,8]);
 room(m,'L / LEGAL\nMEETING',[6,12,8,8],{sides:['solid','glass','frost','solid'],exits:[{side:1,at:14},{side:2,at:10}]});
 room(m,'R / REVIEW\nMEETING',[26,12,8,8],{sides:['solid','solid','frost','glass'],exits:[{side:3,at:14},{side:2,at:30}]});
 m.districts=[{p:[3,10],letter:'L',name:'LEGAL',color:C.blue},{p:[37,10],letter:'R',name:'REVIEW',color:C.electric},{p:[10,26],letter:'C',name:'CLAIMS / work',color:C.purple},{p:[30,26],letter:'A',name:'ARCHIVE / work',color:C.green},{p:[20,7],letter:'H',name:'HEARINGS',color:C.blue}];
 for(const [x,y] of [[2,13],[2,18],[38,13],[38,18],[10,24],[14,24],[26,24],[30,24]])bench(m,x,y);
 low(m,10,16);low(m,30,16);low(m,8,22,3.6,1);low(m,32,22,3.6,1);low(m,28,9,4,1);low(m,17.5,15,1,2);low(m,22.5,15,1,2);low(m,17.5,18.5,2,1,'overhead');
 m.labels.push({p:[20,5],text:'LIFT LOBBY',size:18},{p:[28,10.2],text:'RECEPTION',size:16},{p:[8,21],text:'KITCHEN',size:15},{p:[32,21],text:'COFFEE',size:15});
 baffle(m,[6,10],[8,10]);baffle(m,[32,10],[34,10]);baffle(m,[13.9,20],[13.9,24]);baffle(m,[26.1,20],[26.1,24]);baffle(m,[20,24],[20,28]);
 for(const x of [14,26]){const centre=x===14?13.9:26.1;baffle(m,[centre,4],[centre,5.4]);baffle(m,[centre,6.6],[centre,10]);m.doors.push({p:[x,6],side:x===14?1:3,width:1.2,name:'Lobby return',kind:'door'});}
 m.labels.push({p:[20,25.7],text:'C / A DIVIDER',size:12});
 m.circulation=[[14,6,12,16],[10,4,20,4]];m.circulationHoles=[m.atrium];m.spawnA=[4,24];m.spawnB=[36,24];m.case=[20,21];
 m.loops=[[[15,7],[25,7],[25,21],[15,21],[15,7]]];
 m.lanes=[{name:'1 / window promenade',color:C.electric,p:[[4,24],[4,26.8],[17,26.8],[17,23],[23,23],[23,26.8],[36,26.8],[36,24]]},{name:'2 / court mid',color:C.purple,p:[[4,24],[4.8,24],[4.8,21],[10,21],[10,20],[10,18.5],[12.5,18.5],[12.5,14],[15,14],[15,21],[25,21],[25,14],[27.5,14],[27.5,18.5],[30,18.5],[30,20],[30,21],[35.2,21],[35.2,24],[36,24]]},{name:'3 / inner rooms + lobby',color:C.green,p:[[4,24],[4.8,24],[4.8,21],[10,21],[10,20],[10,18.5],[12.5,18.5],[12.5,14],[15,14],[15,6],[25,6],[25,14],[27.5,14],[27.5,18.5],[30,18.5],[30,20],[30,21],[35.2,21],[35.2,24],[36,24]]}];
 m.nodes=[{p:[20,6],id:'N1',name:'Lift / reception'},{p:[20,21],id:'N2',name:'Case / court'},{p:[8,22.8],id:'N3',name:'Kitchen'},{p:[32,22.8],id:'N4',name:'Coffee'}];
 m.landmarks=[{p:[23,4],id:'K1',name:'Reception lightbox'},{p:[18,8],id:'K2',name:'H / feature stair'},{p:[10,12],id:'K3',name:'L / meeting capsule'}];
 m.carrier=[{name:'F / exposed feature',color:C.blue,p:[[20,21],[20,17.6]],up:'F'},{name:'E1 / sheltered west',color:C.green,p:[[20,21],[15,21],[15,6],[10,6]],up:'E1'},{name:'E2 / sheltered east',color:C.green,p:[[20,21],[25,21],[25,6],[30,6]],up:'E2'},{name:'L / loud lift proposal',color:C.purple,p:[[20,21],[25,21],[25,6],[20,6],[20,4]],up:'LIFT'}];
 return m;
}
function setupC(){
 const m=base('L1_C','SPLIT CORES / FORUM',48,32);m.tagline='Two office neighbourhoods; linked loops; one shared forum.';
 m.coreBands=[[8,8,8,16],[32,8,8,16]];m.atrium=[20,10,8,12];m.feature={x:23,y:12.4,w:2,run:7.2,foot:[24,19.6],top:[24,12.4]};
 service(m,'E1',[8,8,6,8],'stair',[{side:3,at:10},{side:0,at:10}]);service(m,'E2',[34,16,6,8],'stair',[{side:1,at:22},{side:2,at:38}]);
 service(m,'RISERS',[14,8,2,8]);service(m,'RISERS',[32,16,2,8]);service(m,'WC',[8,20,4,4]);service(m,'STORE',[12,20,4,4]);service(m,'LIFT',[32,8,4,4],'lift',[{side:0,at:34,width:1.4,kind:'lift'}]);service(m,'WC / PLANT',[36,8,4,4]);
 room(m,'L / LEGAL\nMEETING',[0,0,8,8],{sides:['none','solid','glass','none'],exits:[{side:1,at:4},{side:2,at:4}]});
 room(m,'R / REVIEW\nMEETING',[40,24,8,8],{sides:['glass','none','none','solid'],exits:[{side:3,at:28},{side:0,at:44}]});
 room(m,'A / ARCHIVE\nQUIET ROOM',[0,24,8,8],{sides:['frost','solid','none','none'],exits:[{side:0,at:4},{side:1,at:28}]});
 room(m,'C / CLAIMS\nQUIET ROOM',[40,0,8,8],{sides:['none','none','frost','solid'],exits:[{side:3,at:4},{side:2,at:44}]});
 m.districts=[{p:[12,3],letter:'L',name:'LEGAL / work',color:C.blue},{p:[36,29],letter:'R',name:'REVIEW / work',color:C.electric},{p:[12,29],letter:'A',name:'ARCHIVE / work',color:C.green},{p:[36,3],letter:'C',name:'CLAIMS / work',color:C.purple},{p:[24,9],letter:'H',name:'HEARINGS',color:C.blue}];
 for(const [x,y] of [[18,3],[30,3],[18,29],[30,29],[3,12],[45,20]])bench(m,x,y);
 for(const [x,y] of [[4,4],[44,28],[4,28],[44,4]])low(m,x,y);
 low(m,17,16,1,2);low(m,31,16,1,2);low(m,21.5,17,1,2);low(m,26.5,15,1,2);low(m,21.5,20.5,2,1,'overhead');
 m.labels.push({p:[13,18],text:'PRINT / COFFEE',size:15},{p:[35,14],text:'RECEPTION',size:15},{p:[24,24.5],text:'FORUM / SOUTH LANDING',size:15});
 baffle(m,[24,0],[24,6]);baffle(m,[24,26],[24,32]);baffle(m,[0,18],[2,18]);baffle(m,[46,14],[48,14]);
 m.circulation=[[18,8,12,16],[8,4,15,2],[25,4,15,2],[21,4,2,4],[25,4,2,4],[21,6,6,2],[8,26,15,2],[25,26,15,2],[21,24,2,4],[25,24,2,4],[21,24,6,2]];m.circulationHoles=[m.atrium];
 m.spawnA=[4,16];m.spawnB=[44,16];m.case=[24,23];
 const north=[[4,16],[6,16],[6,10],[4,10],[4,8],[4,6.5],[6.5,6.5],[6.5,4],[8,4],[10,5],[22,5],[22,7],[26,7],[26,5],[38,5],[40,4],[41.5,4],[41.5,6.5],[44,6.5],[44,8],[44,10],[42,10],[42,16],[44,16]];
 const south=north.map(([x,y])=>[48-x,32-y]).reverse();
 m.lanes=[{name:'1 / north work + suites',color:C.electric,p:north},{name:'2 / forum cross-cut',color:C.purple,p:[[4,16],[6,16],[6,18],[19,18],[19,23],[29,23],[29,14],[42,14],[42,16],[44,16]]},{name:'3 / south work + suites',color:C.green,p:south}];
 m.loops=[[[4,16],[6,16],[6,10],[4,10],[4,8],[4,6.5],[6.5,6.5],[6.5,4],[8,4],[10,5],[19,5],[19,9],[19,23],[19,27],[10,27],[8,28],[6.5,28],[6.5,25.5],[4,25.5],[4,24],[4,22],[6,22],[6,16],[4,16]],[[44,16],[42,16],[42,22],[44,22],[44,24],[44,25.5],[41.5,25.5],[41.5,28],[40,28],[38,27],[29,27],[29,23],[29,9],[29,5],[38,5],[40,4],[41.5,4],[41.5,6.5],[44,6.5],[44,8],[44,10],[42,10],[42,16],[44,16]]];
 m.nodes=[{p:[34,7],id:'N1',name:'Lift / reception'},{p:[24,23],id:'N2',name:'Case / forum'},{p:[15,18],id:'N3',name:'Print / coffee'}];
 m.landmarks=[{p:[36,12],id:'K1',name:'Reception lightbox'},{p:[22,10],id:'K2',name:'H / feature stair'},{p:[8,18],id:'K3',name:'L / core graphic'}];
 m.carrier=[{name:'F / exposed feature',color:C.blue,p:[[24,23],[24,19.6]],up:'F'},{name:'E1 / west enclosed',color:C.green,p:[[24,23],[19,23],[19,7],[10,7],[10,8]],up:'E1'},{name:'E2 / east enclosed',color:C.green,p:[[24,23],[29,23],[29,25],[38,25],[38,24]],up:'E2'},{name:'L / loud lift proposal',color:C.purple,p:[[24,23],[29,23],[29,7],[34,7],[34,8]],up:'LIFT'}];
 return m;
}
const models=[setupA(),setupB(),setupC()];
// Rectangle-union area by an exact coordinate sweep; no raster approximation.
function unionArea(rects){const xs=[...new Set(rects.flatMap(r=>[r[0],r[0]+r[2]]))].sort((a,b)=>a-b);let area=0;
 for(let i=1;i<xs.length;i++){const lo=xs[i-1],hi=xs[i],ys=rects.filter(r=>r[0]<hi&&r[0]+r[2]>lo).map(r=>[r[1],r[1]+r[3]]).sort((a,b)=>a[0]-b[0]);let total=0,last=-Infinity;for(const [a,b]of ys){if(b>last){total+=b-Math.max(a,last);last=b;}}area+=(hi-lo)*total;}return area;}
function rayBox(p,d,r){let lo=-Infinity,hi=Infinity;for(let i=0;i<2;i++){if(Math.abs(d[i])<1e-9){if(p[i]<r[i]||p[i]>r[i]+r[i+2])return null;}else{let a=(r[i]-p[i])/d[i],b=(r[i]+r[i+2]-p[i])/d[i];if(a>b)[a,b]=[b,a];lo=Math.max(lo,a);hi=Math.min(hi,b);}}return hi>=Math.max(lo,0)?[lo,hi]:null;}
function wallRect(w){const t=w.thickness;return Math.abs(w.a[1]-w.b[1])<1e-7?[Math.min(w.a[0],w.b[0]),w.a[1]-t/2,Math.abs(w.a[0]-w.b[0]),t]:[w.a[0]-t/2,Math.min(w.a[1],w.b[1]),t,Math.abs(w.a[1]-w.b[1])];}
function survey(m){
 // Standing eye 1.6m: clear and high/low-frost glass is transparent, low cover is below eye.
 // Axis/full-height faces only; door leaves are held open. Stairs are conservative sight blockers.
 const hard=m.walls.filter(w=>w.type==='solid').map(wallRect);
 hard.push(...m.services.filter(s=>s.kind==='sealed'||s.kind==='stair'||s.kind==='lift').map(s=>s.r));
 hard.push([m.feature.x,m.feature.y,m.feature.w,m.feature.run]); // conservative full-flight projection
 const valid=p=>p[0]>=.55-1e-7&&p[1]>=.55-1e-7&&p[0]<=m.w-.55+1e-7&&p[1]<=m.h-.55+1e-7&&!hard.some(r=>within(p,r,-.01));
 const candidates=[];const shell=[.2,.2,m.w-.4,m.h-.4];
 for(let y=1;y<m.h;y+=.5)for(let x=1;x<m.w;x+=.5){const p=[x,y];if(!valid(p))continue;
  for(let angle=0;angle<180;angle+=2){const rad=angle*Math.PI/180,d=[Math.cos(rad),Math.sin(rad)];let f=rayBox(p,d,shell)?.[1]??0,b=rayBox(p,d.map(v=>-v),shell)?.[1]??0;
   for(const r of hard){let hit=rayBox(p,d,r);if(hit&&hit[0]>=0)f=Math.min(f,hit[0]);hit=rayBox(p,d.map(v=>-v),r);if(hit&&hit[0]>=0)b=Math.min(b,hit[0]);}
   // Endpoints are 0.35m back from wall faces: space for a 0.7m capsule.
   f=Math.max(0,f-.35);b=Math.max(0,b-.35);if(f+b<15)continue;
   const a=[x-d[0]*b,y-d[1]*b],z=[x+d[0]*f,y+d[1]*f];if(!valid(a)||!valid(z))continue;
   candidates.push({a,b:z,length:f+b,angle,mid:[(a[0]+z[0])/2,(a[1]+z[1])/2]});
  }
 }
 candidates.sort((a,b)=>b.length-a.length);const chosen=[];
 for(const c of candidates){if(chosen.every(s=>Math.abs(Math.sin((c.angle-s.angle)*Math.PI/180))>.2||Math.hypot(c.mid[0]-s.mid[0],c.mid[1]-s.mid[1])>6)){chosen.push(c);if(chosen.length===5)break;}}
 return chosen.map((s,i)=>({id:'S'+(i+1),a:s.a,b:s.b,length:s.length}));
}
function rectArea(r){return r[2]*r[3];}
function clip(r,b){const x=Math.max(r[0],b[0]),y=Math.max(r[1],b[1]),x1=Math.min(r[0]+r[2],b[0]+b[2]),y1=Math.min(r[1]+r[3],b[1]+b[3]);return [x,y,Math.max(0,x1-x),Math.max(0,y1-y)];}
for(const m of models){
 m.void=[m.atrium[0],m.feature.y,m.atrium[2],m.feature.run];
 m.landings=[[m.atrium[0],m.atrium[1],m.atrium[2],2.4],[m.atrium[0],m.feature.y+m.feature.run,m.atrium[2],2.4]];
 const circulation=unionArea(m.circulation)-unionArea(m.circulationHoles),core=unionArea(m.services.map(s=>s.r));
 const coreWithinShell=unionArea(m.services.map(s=>clip(s.r,[.2,.2,m.w-.4,m.h-.4])));
 const nia=(m.w-.4)*(m.h-.4)-coreWithinShell-rectArea(m.void);
 m.sightlines=survey(m);
 m.stats={gross:m.w*m.h,core,coreWithinShell,stairBay:rectArea(m.atrium),void:rectArea(m.void),planningNia:nia,primaryCirculation:circulation,circulationPercent:100*circulation/nia,primaryGrossPercent:100*circulation/(m.w*m.h),rooms:m.rooms.length,twoExitRooms:m.rooms.filter(r=>r.exits.length>=2).length,straightCrossSeconds:m.w/9,longestSurveyedSightline:m.sightlines[0].length,looseProps:12,featureFlight3D:Math.hypot(7.2,4.5),escapeStair3D:2*Math.hypot(3.6,2.25)+3.6};
 m.carrier.forEach(r=>r.approachMetres=len(r.p));
}
// Shared drawing display list, rendered both as SVG and PNG without a browser.
let D=[];
function rect(x,y,w,h,fill='none',stroke='none',sw=1){D.push({type:'rect',x,y,w,h,fill,stroke,sw});}
function line(a,b,stroke=C.ink,sw=2,dash=false){D.push({type:'line',a,b,stroke,sw,dash});}
function poly(points,stroke=C.ink,sw=2,fill='none',dash=false){D.push({type:'poly',points,stroke,sw,fill,dash});}
function text(x,y,value,size=22,color=C.ink,align='left',bold=false){D.push({type:'text',x,y,value:String(value),size,color,align,bold});}
function circle(x,y,r,fill,stroke='none',sw=1){D.push({type:'circle',x,y,r,fill,stroke,sw});}
function mr(r,fill,stroke='none',sw=1){const [x,y]=xy(r);rect(x,y,r[2]*PPM,r[3]*PPM,fill,stroke,sw);}
function ml(a,b,c=C.ink,sw=2,dash=false){line(xy(a),xy(b),c,sw,dash);}
function mt(p,t,size=18,c=C.ink,b=false){const[x,y]=xy(p);text(x,y,t,size,c,'center',b);}
function arrow(ps,color,sw=5,dash=false){const p=ps.map(xy);poly(p,color,sw,'none',dash);const a=p[p.length-2],b=p.at(-1),angle=Math.atan2(b[1]-a[1],b[0]-a[0]),l=15;poly([[b[0]-l*Math.cos(angle-.45),b[1]-l*Math.sin(angle-.45)],b,[b[0]-l*Math.cos(angle+.45),b[1]-l*Math.sin(angle+.45)]],color,sw);}
function hatch(r){for(let x=r[0]+.3;x<r[0]+r[2];x+=.65)ml([x,r[1]+.2],[x,r[1]+r[3]-.2],'#C6DDEB',1);}
function drawDoor(d){const [x,y]=xy(d.p),r=d.width*PPM,side=d.side;
 const horizontal=side===0||side===2;const a=horizontal?[x-r/2,y]:[x,y-r/2];const b=horizontal?[a[0],y+(side===0?1:-1)*r]:[x+(side===3?1:-1)*r,a[1]];
 line(a,b,C.muted,1.4);line(b,horizontal?[x+r/2,y]:[x,y+r/2],C.line,1,true);
 // Clear opening tick marks remain visible under flow overlays.
 if(horizontal){line([x-r/2,y-4],[x-r/2,y+4],C.ink,2);line([x+r/2,y-4],[x+r/2,y+4],C.ink,2);}else{line([x-4,y-r/2],[x+4,y-r/2],C.ink,2);line([x-4,y+r/2],[x+4,y+r/2],C.ink,2);}
}
function stair(m,s){const[x,y,w,h]=s.r;mr(s.r,'#E7EAF1');
 if(s.kind==='lift'){mr([x+.35,y+.35,w-.7,h-.7],'#C1CCD9',C.ink,1);mt([x+w/2,y+h/2],'LIFT',19,C.ink,true);return;}
 for(let i=0;i<=12;i++){ml([x+.2,y+2.2+i*.3],[x+2.2,y+2.2+i*.3],C.muted,1);ml([x+3.8,y+2.2+i*.3],[x+5.8,y+2.2+i*.3],C.muted,1);}
 arrow([[x+1.2,y+6],[x+1.2,y+2.4],[x+4.8,y+2.4],[x+4.8,y+6]],C.blue,2);
 mt([x+3,y+1.2],s.name+' / UP',18,C.blue,true);mt([x+3,y+7], '2m flights',15);
}
function header(m,kind){rect(0,0,WIDTH,HEIGHT,'#FFFFFF');text(140,65,'CLOSE THE DEAL  /  OFFICE LAYOUT STUDY',24,C.muted);text(140,122,m.id+'   '+m.title,43,C.ink,'left',true);text(140,173,kind+'  •  TARGET CONCEPT  •  15 NOV 2026',21,C.blue);text(140,205,m.tagline,20,C.muted);}
function shell(m){
 mr([0,0,m.w,m.h],'#FAFBFD');for(const r of m.coreBands)mr(r,'#EFF2F7');
 for(const r of m.circulation)mr(r,'#E4ECFB');for(const r of m.circulationHoles)mr(r,'#EFF2F7');
 mr(m.atrium,'#F0F5FC');mr(m.void,C.void);hatch(m.void);
 for(const r of m.landings)mr(r,'#DCE8F9');
 for(const r of m.rooms)mr(r.r,'#F2F7F9');
 for(const s of m.services){mr(s.r,C.core);if(s.kind==='stair'||s.kind==='lift')stair(m,s);else{const[x,y,w,h]=s.r;mt([x+w/2,y+h/2],s.name,Math.min(17,w*6),C.muted,true);mt([x+w/2,y+h/2+.5],'SEALED',12,C.muted);}}
 for(let x=0;x<=m.w;x+=4){ml([x,0],[x,m.h],'#CAD3E1',1);mt([x,-.65],x,16,C.muted);}
 for(let y=0;y<=m.h;y+=4){ml([0,y],[m.w,y],'#CAD3E1',1);mt([-.8,y],y,16,C.muted);}
 // Outer glazing: 200mm envelope, 600mm sill; north/south and unoccupied side bays.
 mr([0,0,m.w,.2],C.ink);mr([0,m.h-.2,m.w,.2],C.ink);mr([0,0,.2,m.h],C.ink);mr([m.w-.2,0,.2,m.h],C.ink);
 for(let x=0;x<m.w;x+=4){ml([x+.2,.10],[x+3.8,.10],C.glass,3);ml([x+.2,m.h-.10],[x+3.8,m.h-.10],C.glass,3);}
 for(let y=0;y<m.h;y+=4){if(!m.services.some(s=>s.r[0]===0&&y>=s.r[1]&&y<s.r[1]+s.r[3]))ml([.10,y+.2],[.10,y+3.8],C.glass,3);if(!m.services.some(s=>s.r[0]+s.r[2]===m.w&&y>=s.r[1]&&y<s.r[1]+s.r[3]))ml([m.w-.10,y+.2],[m.w-.10,y+3.8],C.glass,3);}
 for(const w of m.walls){ml(w.a,w.b,w.type==='solid'?C.ink:w.type==='glass'?C.glass:C.frost,w.thickness*PPM,w.type==='frost');if(w.type==='glass')ml(w.a,w.b,'#FFFFFF',1);}
 for(const b of m.benches){mr(b.r,'#CCD6E6',C.muted,1);const[x,y,w,h]=b.r;ml([x,y+h/2],[x+w,y+h/2],C.ink,2);for(const xx of [.55,1.85]){mr([x+xx-.22,y+.15,.44,.17],C.ink);mr([x+xx-.22,y+h-.32,.44,.17],C.ink);}}
 for(const b of m.covers){if(b.kind==='overhead'){mr(b.r,'#E5EAF2',C.muted,1);const[x,y,w,h]=b.r;mt([x+w/2,y+h/2],'UNDER SLAB',10,C.ink);}else mr(b.r,C.low,C.ink,1);}
 const f=m.feature;mr([f.x,f.y,f.w,f.run],'#FFFFFF',C.blue,2);for(let i=0;i<=24;i++)ml([f.x,f.y+i*.3],[f.x+f.w,f.y+i*.3],C.muted,1);arrow([f.foot,f.top],C.blue,3);mt([f.x+f.w/2,f.y+f.run/2],'F',24,C.blue,true);
 const[a,b,w,h]=m.void;
 ml([a,b],[a,b+h],C.glass,2,true);ml([a+w,b],[a+w,b+h],C.glass,2,true);
 for(const y of [b,b+h]){ml([a,y],[f.x,y],C.glass,2,true);ml([f.x+f.w,y],[a+w,y],C.glass,2,true);}
 for(const d of m.doors)drawDoor(d);
 for(const l of m.labels){const lines=l.text.split('\n');lines.forEach((t,i)=>mt([l.p[0],l.p[1]+(i-(lines.length-1)/2)*.55],t,l.size));}
 for(const z of m.districts){mt(z.p,z.letter,34,z.color,true);mt([z.p[0],z.p[1]+.67],z.name,15,z.color,true);}
 mt([m.atrium[0]+m.atrium[2]/2,m.atrium[1]+.65],'ATRIUM / NORTH LANDING',14,C.blue,true);
 mt([m.atrium[0]+m.atrium[2]/2,m.atrium[1]+1.2],'8 × 12m bay / 8 × 7.2m void',13,C.muted);
}
function dimension(m){ml([0,-1.25],[m.w,-1.25],C.ink,1);ml([0,-1.45],[0,-1.05],C.ink,1);ml([m.w,-1.45],[m.w,-1.05],C.ink,1);mt([m.w/2,-1.55],m.w+'m overall',18,C.ink,true);mt([m.w+.9,m.h/2],m.h+'m',18,C.ink,true);}
function footer(m){const y=OY+m.h*PPM+70;const x=OX;for(let i=0;i<8;i++)rect(x+i*40,y,40,14,i%2?C.white:C.ink,C.ink,1);text(x,y+33,'0',16);text(x+160,y+33,'4',16);text(x+320,y+33,'8m',16);text(x+420,y+9,'40 px = 1m at native resolution  /  4m grid  /  NW origin',18,C.muted);text(x,y+72,'Unity mapping: (x, y-plan) → (X, height, −y-plan)  •  doors 1.2 × 2.4m  •  walls 0.2m / partitions 0.1m',18,C.muted);text(x,y+106,'Blue hatch = upper void; pale blue ends = 2.4m landings; dashed guard opens at the stair ends.',18,C.muted);}
function sidebar(m,kind){const x=2170;line([2110,240],[2110,1720],C.line,1);text(x,255,kind==='flow'?'READ THE FLOW':'READ THE PLAN',24,C.ink,'left',true);
 const key=[['Solid wall / hard cover',C.ink,false],['Clear glass / not cover',C.glass,false],['Frost header + low band',C.frost,true],['1.1m fixed cover',C.low,false],['Primary circulation', '#A3BAE6',false],['Upper void / guard',C.glass,true]];
 key.forEach(([t,c,d],i)=>{line([x,307+i*42],[x+52,307+i*42],c,7,d);text(x+70,307+i*42,t,18);});
 let y=615;const s=m.stats;for(const [a,b]of [['FLOOR',`${m.w} × ${m.h}m / ${s.gross}m²`],['PRIMARY CIRCULATION',`${s.primaryCirculation.toFixed(0)}m² / ${s.circulationPercent.toFixed(1)}%`],['PLANNING NIA PROXY',`${s.planningNia.toFixed(1)}m²`],['TWO-EXIT PLAY ROOMS',`${s.twoExitRooms} / ${s.rooms}`],['DIRECT WIDTH / 9m/s',`${s.straightCrossSeconds.toFixed(2)}s (no obstacles)`],['LOOSE PROP BUDGET','12 / 16 per floor']]){text(x,y,a,15,C.muted,'left',true);text(x,y+30,b,24,C.ink,'left',true);y+=87;}
 if(kind==='flow'){
 text(x,1175,'LANES / not floor markings',18,C.ink,'left',true);m.lanes.forEach((l,i)=>{line([x,1210+i*36],[x+35,1210+i*36],l.color,5);text(x+50,1210+i*36,l.name,17);});
 text(x,1340,'SIGHTLINE SURVEY / 1.6m eye',18,C.ink,'left',true);m.sightlines.forEach((s,i)=>text(x,1378+i*31,s.id+'   '+s.length.toFixed(1)+'m',20,C.muted));text(x,1555,'Open doors; glass passes shots.',16,C.muted);text(x,1581,'Five distinct longest sampled axes.',16,C.muted);text(x,1607,'Conservative stair blocker; see method.',16,C.muted);
 }else{
 text(x,1175,'FIXED SHELL / EVERY FLOOR',18,C.ink,'left',true);['F: 2m × 7.2m / rise 4.5m','E1 + E2: 6 × 8m U-stair boxes','Two 2m flights + 2m landings','Lift: 4 × 4m fixed shaft','Slab 0.2m / soffit 4.3m','Services: sealed dressing, no loot'].forEach((t,i)=>text(x,1215+i*34,t,17,C.muted));text(x,1475,'DISTRICT COLOURS',18,C.ink,'left',true);text(x,1510,'L / H cobalt   R electric blue',17,C.muted);text(x,1542,'C muted violet   A cool teal',17,C.muted);text(x,1580,'Diagram tints identify districts;',16,C.muted);text(x,1607,'built architecture stays white/cobalt.',16,C.muted);
 }
 text(x,1680,'PROPOSAL • NOT A BUILD DRAWING',16,C.blue,'left',true);
}
function flow(m){
 for(const p of m.loops){poly(p.map(xy),'#FFFFFF',12,'none',true);poly(p.map(xy),C.blue,4,'none',true);}
 for(const l of m.lanes){poly(l.p.map(xy),'#FFFFFF',9);arrow(l.p,l.color,4);}
 // Sightlines carry numbered badges; length values are kept in the uncluttered sidebar.
 for(const s of m.sightlines){ml(s.a,s.b,'#6F7C91',1.7,true);const p=xy([(s.a[0]+s.b[0])/2,(s.a[1]+s.b[1])/2]);rect(p[0]-19,p[1]-13,38,26,'#FFFFFF','#A0ABC0',1);text(p[0],p[1],s.id,14,C.muted,'center',true);}
 for(const n of m.nodes){const[x,y]=xy(n.p);circle(x,y,22,'#FFFFFF',C.blue,3);text(x,y,n.id,15,C.blue,'center',true);}
 for(const k of m.landmarks){const[x,y]=xy(k.p);poly([[x,y-14],[x+14,y],[x,y+14],[x-14,y],[x,y-14]],C.blue,2,'#FFFFFF');text(x,y,k.id,10,C.blue,'center',true);}
 for(const r of m.carrier)arrow(r.p,r.color,3,true);
 for(const [p,t]of [[m.spawnA,'FIRM A'],[m.spawnB,'FIRM B']]){const[x,y]=xy(p);rect(x-51,y-17,102,34,C.ink);text(x,y,t,17,'#FFFFFF','center',true);}
 const[cx,cy]=xy(m.case);rect(cx-15,cy-12,30,24,C.blue,'#FFFFFF',2);text(cx,cy,'B',16,'#FFFFFF','center',true);
 const y=OY+m.h*PPM+220;
 text(140,y,'DASHED BLUE = main loop    CIRCLES = nodes    DIAMONDS = landmarks    B = shared briefcase start',17,C.blue);
 text(140,y+30,'Dashed arrows: F exposed stair  /  E1–E2 enclosed escape stairs  /  L loud lift (availability undecided).',17,C.muted);
}
function escapeXML(s){return String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');}
function svg(){return `<svg xmlns="http://www.w3.org/2000/svg" width="${WIDTH}" height="${HEIGHT}" viewBox="0 0 ${WIDTH} ${HEIGHT}">`+D.map(d=>{
 const stroke=`stroke="${d.stroke??'none'}" stroke-width="${d.sw??1}" ${d.dash?'stroke-dasharray="9 7"':''}`;
 if(d.type==='rect')return `<rect x="${d.x}" y="${d.y}" width="${d.w}" height="${d.h}" fill="${d.fill}" ${stroke}/>`;
 if(d.type==='circle')return `<circle cx="${d.x}" cy="${d.y}" r="${d.r}" fill="${d.fill}" ${stroke}/>`;
 if(d.type==='line')return `<line x1="${d.a[0]}" y1="${d.a[1]}" x2="${d.b[0]}" y2="${d.b[1]}" ${stroke}/>`;
 if(d.type==='poly')return `<polyline points="${d.points.map(p=>p.join(',')).join(' ')}" fill="${d.fill}" ${stroke}/>`;
 return `<text x="${d.x}" y="${d.y}" fill="${d.color}" font-family="Arial,sans-serif" font-size="${d.size}" font-weight="${d.bold?'700':'400'}" text-anchor="${d.align==='center'?'middle':d.align==='right'?'end':'start'}" dominant-baseline="central">${escapeXML(d.value)}</text>`;
 }).join('\n')+'</svg>';}
function save(m,name){const folder=path.join(ROOT,m.id);fs.mkdirSync(folder,{recursive:true});fs.writeFileSync(path.join(folder,name+'.svg'),svg());fs.writeFileSync(path.join(folder,name+'.draw.json'),JSON.stringify({width:WIDTH,height:HEIGHT,commands:D}));}
function stack(m){D=[];header(m,'STACK / registered plans + measured vertical section');
 // Registered mini-plans: same x/y and scale on all floors, with a fixed void and core.
 const left=165,top=310,sc=10,step=345;
 const fitouts=['01 / LEGAL + CLAIMS','02 / REVIEW + ARCHIVE','03 / HEARINGS + EXECUTIVE','RF / OPEN ROOF'];
 const styles=[C.blue,C.electric,C.purple,C.muted];
 for(let f=0;f<4;f++){
  const y=top+f*step;const tx=p=>[left+p[0]*sc,y+p[1]*sc];
  rect(left,y,m.w*sc,m.h*sc,'#F8FAFD',C.ink,2);
  for(const r of m.coreBands)rect(left+r[0]*sc,y+r[1]*sc,r[2]*sc,r[3]*sc,C.core);
  if(f>0)rect(left+m.void[0]*sc,y+m.void[1]*sc,m.void[2]*sc,m.void[3]*sc,'#FFFFFF',C.glass,2);
  for(const r of m.landings)rect(left+r[0]*sc,y+r[1]*sc,r[2]*sc,r[3]*sc,'#DCE8F9');
  for(const s of m.services){const r=s.r;rect(left+r[0]*sc,y+r[1]*sc,r[2]*sc,r[3]*sc,s.kind==='stair'?C.blue:s.kind==='lift'?C.purple:'#CBD3E1');if(['stair','lift'].includes(s.kind))text(left+(r[0]+r[2]/2)*sc,y+(r[1]+r[3]/2)*sc,s.name,12,'#FFFFFF','center',true);}
  const a=m.feature;rect(left+a.x*sc,y+a.y*sc,a.w*sc,a.run*sc,C.electric);
  // Department fit-outs vary inside the same rooms and workplace bands.
  if(f<3){
   const benches=f===1?m.benches:m.benches.filter((_,i)=>i%2===0);
   for(const b of benches){const r=b.r;rect(left+r[0]*sc,y+r[1]*sc,r[2]*sc,r[3]*sc,styles[f]);}
   for(const rm of m.rooms){const r=rm.r;rect(left+r[0]*sc,y+r[1]*sc,r[2]*sc,r[3]*sc,'none',C.glass,1);const tableW=f===2?4:2;rect(left+(r[0]+r[2]/2-tableW/2)*sc,y+(r[1]+r[3]/2-.5)*sc,tableW*sc,sc,styles[f]);}
  }
  text(left+m.w*sc+35,y+20,fitouts[f],23,styles[f],'left',true);
 text(left+m.w*sc+35,y+56,f===0?'Arrival / meeting suites':f===1?'Work benches / copy suites':f===2?'Boardroom tables / quiet suites':'Case arrival / roof goal',18,C.muted);
 text(left+m.w*sc+35,y+90,f===3?'Guarded void + stair landings':'Same core, void, shell and ports',17,C.muted);
  if(f===3)text(left+m.w*sc+35,y+124,'Roof pool / range omitted from comparison',17,C.muted);
 }
 // Mini-plan rows are not vertically scaled: the section below is.
 const sx=1470,sy=1370,s=42,atriumW=8,coreW=6,sectionW=24;
 text(1420,290,'SECTION THROUGH FEATURE STAIR',25,C.ink,'left',true);text(1420,330,'42 px = 1m in both section axes',18,C.muted);
 for(let f=0;f<4;f++){
  const y=sy-f*4.5*s;rect(sx,y,sectionW*s,.2*s,C.core,C.muted,1);
  // Remove slab over 8m void on upper levels; retain continuous lower floor.
  if(f>0)rect(sx+8.4*s,y,7.2*s,.2*s,'#FFFFFF');
  text(1420,y-22,(f===3?'RF':String(f+1).padStart(2,'0'))+'  +'+(f*4.5).toFixed(1)+'m',18,C.ink,'right',true);
  if(f<3){
   line([sx+8.4*s,y],[sx+15.6*s,y-4.5*s],C.blue,6);
   text(sx+20*s,y-2.25*s,'4.5m',18,C.blue,'center',true);
   line([sx+20*s,y],[sx+20*s,y-4.5*s],C.blue,1,true);
   line([sx,y-4.3*s],[sx+7.5*s,y-4.3*s],C.muted,1,true);
   rect(sx+2*s,y-1.1*s,2*s,1.1*s,C.low);
  }
 }
 line([sx+1*s,sy],[sx+1*s,sy-13.5*s],C.purple,6,true);text(sx+1*s,sy+45,'LIFT',18,C.purple,'center',true);
 line([sx+22*s,sy],[sx+22*s,sy-13.5*s],C.green,6,true);text(sx+22*s,sy+45,'E1 / E2',18,C.green,'center',true);
 text(1420,1510,'Feature: 7.2m run / 4.5m rise / 24 × 187.5mm',20,C.ink);
 text(1420,1550,'2m clear flights; 2.4m end landings in the 12m bay.',19,C.muted);
 text(1420,1590,'E1/E2: two 3.6m runs in 6 × 8m boxes.',19,C.muted);
 text(1420,1630,'2m landings; 2m clear flights; ramp collision required.',19,C.muted);
 text(1420,1670,'0.2m slab / 4.3m soffit / three floors + roof at 13.5m.',19,C.muted);
 text(1420,1710,'Lift stops at every level are a proposal; no skip rule set.',19,C.muted);
 text(165,1800,'Mini-plans 10 px/m; all floors registered. Section 42 px/m. Egress/fire-code compliance is not certified.',19,C.muted);
 save(m,'stack');
}
for(const m of models){for(const kind of ['plan','flow']){D=[];header(m,kind==='plan'?'PLAN / measured lower floor':'FLOW / lanes, vertical routes and sightline survey');shell(m);dimension(m);footer(m);sidebar(m,kind);if(kind==='flow')flow(m);save(m,kind);}stack(m);}
fs.writeFileSync(path.join(ROOT,'layouts_L1.json'),JSON.stringify({units:'metres',origin:'NW; x east; y south = Unity -Z',ppm:PPM,doorAssumption:'ordinary clear opening 1.2 x 2.4m; lift 1.4 x 2.2m',models},null,2));
fs.writeFileSync(path.join(ROOT,'metrics_L1.json'),JSON.stringify(models.map(m=>({id:m.id,...m.stats,sightlines:m.sightlines,carrier:m.carrier,lanes:m.lanes.map(l=>({name:l.name,length:len(l.p),sprintSeconds:len(l.p)/9}))})),null,2));
console.log(JSON.stringify(models.map(m=>({id:m.id,...m.stats,sightlines:m.sightlines.map(s=>s.length.toFixed(1))})),null,2));
