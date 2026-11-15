// Reuses L1 definitions in an isolated VM; never executes its output loop.
// Run from repo root: node ArtSource/Kit/concepts/Layout/L2/draw_L2.mjs
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const out=path.dirname(fileURLToPath(import.meta.url)), root=path.dirname(out);
const cachedPath=path.join(out,'layouts.json');
const cached=fs.existsSync(cachedPath)?JSON.parse(fs.readFileSync(cachedPath,'utf8')).models[0]:null;
const source=fs.readFileSync(path.join(root,'draw_L1.mjs'),'utf8');
const shared=source.slice(source.indexOf('const C='),source.indexOf('const models='));
let measurement=source.slice(source.indexOf('// Rectangle-union'),source.indexOf('// Shared drawing'));
measurement=measurement.replace('m.void=[m.atrium[0],m.feature.y,m.atrium[2],m.feature.run];','m.void=[16,12.4,8,7.2];').replace('m.landings=[[m.atrium[0],m.atrium[1],m.atrium[2],2.4],[m.atrium[0],m.feature.y+m.feature.run,m.atrium[2],2.4]];','m.landings=[[16,10,8,2.4],[16,19.6,8,2.4]];');
measurement=measurement.replace('m.landings=[[16,10,8,2.4],[16,19.6,8,2.4]];','m.landings=[[16,10,8,2.4],[16,19.6,8,2.4],[14.4,12.4,2,2],[23.6,12.4,2,2]];').replaceAll('rectArea(m.void)','(rectArea(m.void)-1.6)').replace('escapeStair3D:2*Math.hypot(3.6,2.25)+3.6','escapeStair3D:2*Math.hypot(3.6,2.25)+3.6+4');
measurement=measurement.replace('m.sightlines=survey(m);','m.sightlines=CACHED && surveyKey(m)===surveyKey(CACHED) ? CACHED.sightlines : survey(m);');
let drawing=source.slice(source.indexOf('let D=[];'),source.lastIndexOf("for(const m of models){for(const kind"));
const flightStart=drawing.indexOf('const f=m.feature;mr('),flightEnd=drawing.indexOf('const[a,b,w,h]=m.void;',flightStart);
drawing=drawing.slice(0,flightStart)+`const f=m.feature;mr([f.x,f.y,f.w,f.run],'#FFFFFF',C.blue,2);for(let i=0;i<=24;i++)ml([f.x+i*.3,f.y],[f.x+i*.3,f.y+2],C.muted,1);arrow([f.foot,f.top],C.blue,3);mt([20,13.4],'F',24,C.blue,true);`+drawing.slice(flightEnd);
function setupL2(){
 const m=base('L2','TWIN-CORE COURT',40,32);
 m.tagline='A dependable ring; a visible court; two inhabited cores; three routes through the office.';
 m.coreBands=[[8,6,6,18],[26,8,6,18]];
 m.atrium=[16,10,8,12];
 m.feature={x:16.4,y:12.4,w:7.2,run:2,foot:[23.6,13.4],top:[16.4,13.4],orientation:'east-west; west end up'};
 // Three opaque sides, one full-width arrival aperture; no second stair exit.
 room(m,'E1',[8,6,6,8],{sides:['solid','solid','none','solid'],service:true,kind:'stair',label:false});
 room(m,'E2',[26,18,6,8],{sides:['none','solid','solid','solid'],service:true,kind:'stair',label:false});
 service(m,'LIFT',[18,0,4,4],'lift',[{side:2,at:20,width:1.4,kind:'lift'}]);
 service(m,'RISER',[12,22,2,2]); service(m,'RISER',[26,8,2,2]);
 room(m,'PRINT / COPY',[8,14,6,4],{sides:['none','glass','solid','solid'],exits:[{side:3,at:15.2},{side:1,at:16.8}]});
 room(m,'KITCHEN',[8,18,6,4],{sides:['none','glass','solid','solid'],exits:[{side:3,at:20},{side:1,at:20},{side:2,at:10}]});
 room(m,'WASHROOM',[8,22,4,2],{sides:['solid','solid','solid','frost'],exits:[{side:3,at:23},{side:0,at:10}],label:false});
 room(m,'SERVER / ARCHIVE',[26,14,6,4],{sides:['solid','solid','none','glass'],exits:[{side:3,at:15.2},{side:1,at:16.8}]});
 room(m,'COFFEE KITCHEN',[26,10,6,4],{sides:['solid','solid','none','glass'],exits:[{side:3,at:12},{side:1,at:12},{side:0,at:30}]});
 room(m,'WASHROOM',[28,8,4,2],{sides:['solid','frost','solid','solid'],exits:[{side:1,at:9},{side:2,at:30}],label:false});
 room(m,'L / LEGAL\nCONFERENCE',[0,8,4,8],{sides:['solid','glass','solid','none'],exits:[{side:1,at:10},{side:1,at:14}]});
 room(m,'A / BACK\nOFFICE',[0,20,4,8],{sides:['frost','solid','glass','none'],exits:[{side:0,at:2},{side:1,at:24}]});
 room(m,'R / REVIEW\nCONFERENCE',[36,16,4,8],{sides:['solid','none','solid','glass'],exits:[{side:3,at:18},{side:3,at:22}]});
 room(m,'C / BACK\nOFFICE',[36,4,4,8],{sides:['glass','none','frost','solid'],exits:[{side:3,at:8},{side:2,at:38}]});
 // Two small offsets stop the long ring from becoming one firing corridor.
 baffle(m,[20,4],[20,5]); baffle(m,[20,27],[20,32]);
 baffle(m,[4,0],[4,2]); baffle(m,[36,30],[36,32]);
 baffle(m,[0,19],[2,19]); baffle(m,[38,13],[40,13]);
 // A clear 2m checkpoint port between the east conference and back office.
 m.bridgePort=[40,14.8]; m.rotatedBridgePort=[0,17.2];
 m.circulation=[[5,3,13,2],[22,3,13,2],[16,5,2,2],[16,5,8,2],[22,5,2,2],
  [5,5,2,22],[33,5,2,22],[5,27,13,2],[22,27,13,2],[16,25,2,2],[16,25,8,2],[22,25,2,2]];
 m.circulationHoles=[];
 m.spawnA=[4,4]; m.spawnB=[36,28]; m.entryA=[16,8]; m.entryB=[24,24]; m.case=[20,16];
 m.loops=[[[6,4],[17,4],[17,6],[23,6],[23,4],[34,4],[34,28],[23,28],[23,26],[17,26],[17,28],[6,28],[6,4]]];
 m.lanes=[
  {name:'1 / Window Hall',color:C.electric,p:[[4,4],[6,4],[17,4],[17,6],[23,6],[23,4],[34,4],[34,28],[36,28]]},
  {name:'2 / Court',color:C.purple,p:[[4,4],[16,4],[16,8],[20,16],[24,24],[24,28],[36,28]]},
  {name:'3 / Copy + Archive',color:C.green,p:[[4,4],[6,4],[6,15.2],[8,15.2],[9.5,15.2],[9.5,16.8],[14,16.8],[16,16.8],[16,23],[24,23],[24,15.2],[26,15.2],[30.5,15.2],[30.5,16.8],[32,16.8],[34,16.8],[34,28],[36,28]]}
 ];
 m.nodes=[{p:[20,6],id:'N1',name:'Lift Lobby'},{p:[20,16],id:'N2',name:'Court / Case'},
  {p:[6,20],id:'N3',name:'Kitchen'},{p:[34,12],id:'N4',name:'Coffee'},
  {p:[16,8],id:'N5',name:'Legal Entry'},{p:[24,24],id:'N6',name:'Review Entry'}];
 m.landmarks=[{p:[15,6],id:'K1',name:'Lift lightbox'},{p:[20,13.4],id:'K2',name:'Feature + glass bridge'},
  {p:[3,6],id:'K3',name:'Legal reception'},{p:[37,26],id:'K4',name:'Review reception'}];
 m.carrier=[
  {name:'F / exposed feature',color:C.blue,p:[[20,16],[24.6,16],[24.6,13.4],[23.6,13.4]],up:'F'},
  {name:'E1 / print flank',color:C.green,p:[[20,16],[15,16],[15,16.8],[14,16.8],[9.2,16.8],[9.2,14]],up:'E1'},
  {name:'E2 / archive flank',color:C.green,p:[[20,16],[25,16],[25,15.2],[26,15.2],[30.8,15.2],[30.8,18]],up:'E2'},
  {name:'L / shaft reservation only',color:C.purple,p:[[20,16],[20,6],[20,4]],up:'LIFT'}
 ];
 m.caseApproaches=[{name:'Firm A to case',p:[[4,4],[16,4],[16,8],[20,16]]},
  {name:'Firm B to case',p:[[36,28],[24,28],[24,24],[20,16]]}];
 m.rotations=[{name:'Print to archive via court',p:[[14,16.8],[15,16.8],[15,16],[25,16],[25,15.2],[26,15.2]]},
  {name:'Enclosed stair entries',p:[[9.2,14],[9.2,16.8],[14,16.8],[15,16.8],[15,16],[25,16],[25,15.2],[26,15.2],[30.8,15.2],[30.8,18]]},
  {name:'Court entry A to entry B',p:[[16,8],[20,16],[24,24]]}];
 m.gates=[{id:'G1',a:[8,14],b:[14,14],name:'E1 south arrival'},
  {id:'G2',a:[26,18],b:[32,18],name:'E2 north arrival'},
  {id:'GF',a:[16.4,12.4],b:[16.4,14.4],name:'Feature west arrival'}];
 m.bridge=[16,10,8,2.4]; m.canopy=[18,20,4,2];
 for(const [x,y]of [[10,2],[14,2],[26,30],[30,30],[2,2],[38,30]])bench(m,x,y);
 for(const [x,y,w,h]of [[2,12,2,1],[38,20,2,1],[2,24,2,1],[38,8,2,1],
  [30,14.2,2,1],[12,21.2,3,1],[28,10.8,3,1],[17,17,1,2],[22.5,18,1,2],
  [3,5.8,2,1],[37,26.2,2,1]])low(m,x,y,w,h);
 // Wardrobe/storage returns give occasional full-height cover, with office purpose.
 baffle(m,[15,10],[15,12]); baffle(m,[25,20],[25,22]);
 baffle(m,[11,14.4],[11,15.6]); baffle(m,[29,16.4],[29,17.6]);
 m.labels.push({p:[10,23],text:'WC',size:14},{p:[30,9],text:'WC',size:14},
  {p:[20,8],text:'LIFT LOBBY > COURT',size:16},{p:[20,23],text:'SOUTH CANOPY / 4.3m clear',size:13},
  {p:[6,30],text:'WINDOW HALL',size:14},{p:[34,2],text:'WINDOW HALL',size:14});
 m.districts=[{p:[7,1],letter:'L',name:'LEGAL / WORK',color:C.blue},
  {p:[33,31],letter:'R',name:'REVIEW / WORK',color:C.electric},
  {p:[2,29.5],letter:'A',name:'ARCHIVE',color:C.green},
  {p:[38,2],letter:'C',name:'CLAIMS',color:C.purple},
  {p:[17,18],letter:'H',name:'COURT',color:C.blue}];
 m.props=[[11,3],[15,3],[25,29],[29,29],[1,6],[39,26],[3,17],[37,15],[11,19],[29,13],[2,26],[38,6]];
 return m;
}
const custom=String.raw`
const l1Shell=shell;
const l1Stair=stair;
stair=function(m,s){if(s.name!=='E2')return l1Stair(m,s);const previous=D;D=[];l1Stair(m,{...s,r:[8,6,6,8]});const commands=D;D=previous;
 const turn=p=>[2*OX+40*PPM-p[0],2*OY+32*PPM-p[1]];
 for(const c of commands){if(c.type==='rect'){[c.x,c.y]=turn([c.x+c.w,c.y+c.h]);}else if(c.type==='line'){c.a=turn(c.a);c.b=turn(c.b);}else if(c.type==='poly'){c.points=c.points.map(turn);}else if(c.type==='text'){[c.x,c.y]=turn([c.x,c.y]);}D.push(c);}
};
header=function(m,kind){rect(0,0,WIDTH,HEIGHT,'#FFFFFF');text(140,65,'SEAL THE DEAL / OFFICE LAYOUT STUDY',24,C.muted);text(140,122,'L2   TWIN-CORE COURT',43,C.ink,'left',true);text(140,173,kind+' / TARGET CONCEPT / 15 NOV 2026',21,C.blue);text(140,205,m.tagline,20,C.muted);};
shell=function(m){
 l1Shell(m);
 D=D.filter(d=>!(d.type==='text'&&(d.value==='SEALED'||d.value.includes('ATRIUM / NORTH')||d.value.includes('bay /'))));
 // Clear cage faces have no ballistic cover; they prevent an upper-floor gate bypass.
 for(const y of [12.4,14.4])ml([16.4,y],[23.6,y],C.cyan,4,true);
 ml([23.6,12.4],[23.6,14.4],C.cyan,4,true);
 const r=m.bridge;mr(r,'none',C.electric,3);mt([20,11],'UPPER GLASS BRIDGE',12,C.blue,true);
 mr(m.canopy,'none',C.ink,3);mt([20,21],'SOLID CANOPY',12,C.ink,true);
 for(const g of m.gates){ml(g.a,g.b,'#C0281E',7,true);mt([g.a[0]+.8,g.a[1]-.45],g.id,16,'#C0281E',true);}
 for(const [i,p]of m.props.entries()){const[x,y]=xy(p);circle(x,y,6,C.purple,'#FFFFFF',1);}
 for(const [p,t]of [[m.spawnA,'A / RECEPTION'],[m.spawnB,'B / RECEPTION']]){mt(p,t,16,C.blue,true);}
 for(const [p,t]of [[m.entryA,'A ENTRY'],[m.entryB,'B ENTRY']]){mt([p[0],p[1]+.7],t,13,C.blue,true);}
 const[cx,cy]=xy(m.case);rect(cx-14,cy-10,28,20,C.blue,'#FFFFFF',2);text(cx,cy,'B',14,'#FFFFFF','center',true);
 arrow([[34,14.8],[40,14.8]],C.purple,2,true);mt([37,13.5],'BRIDGE PORT*',12,C.purple,true);
};
sidebar=function(m,kind){const x=1890;line([1830,240],[1830,1720],C.line,1);text(x,255,kind==='flow'?'READ THE FLOW':'READ THE PLAN',24,C.ink,'left',true);
 const rows=[['40 x 32m / 1,280 sq m','15-22% circulation target'],
  [m.stats.primaryCirculation.toFixed(0)+' sq m / '+m.stats.circulationPercent.toFixed(1)+'% ring','Planning NIA proxy '+m.stats.planningNia.toFixed(1)+' sq m'],
  [m.stats.twoExitRooms+' / '+m.stats.rooms+' two-exit rooms','12 / 16 loose props; purple dots'],
  ['4.44s across width at 9m/s','Unobstructed lower bound only']];
 rows.forEach((r,i)=>{text(x,310+i*77,r[0],23,C.ink,'left',true);text(x,340+i*77,r[1],17,C.muted);});
 let y=650;
 const key=[['Solid wall / fixed hard cover',C.ink,false],['Glass / information, no shot cover',C.glass,false],['Frost bands / clear at eye height',C.frost,true],['Low cover / 1.1m high',C.low,false],['G1 G2 GF / upper gate apertures','#C0281E',true],['Cyan / continuous clear stair cage',C.cyan,true],['Blue outline / bridge ABOVE',C.electric,false],['Black outline / canopy ABOVE',C.ink,false]];
 key.forEach(([t,c,d],i)=>{line([x,y+i*34],[x+40,y+i*34],c,5,d);text(x+55,y+i*34,t,17);});
 if(kind==='flow'){
 text(x,960,'THREE OFFICE ROUTES',18,C.ink,'left',true);m.lanes.forEach((l,i)=>text(x,995+i*30,l.name+' / '+(len(l.p)/9).toFixed(2)+'s',18,l.color));
 text(x,1110,'FIVE LONGEST SAMPLED AXES',18,C.ink,'left',true);m.sightlines.forEach((s,i)=>text(x,1145+i*29,s.id+' / '+s.length.toFixed(1)+'m',19,C.muted));
 text(x,1310,'CASE > UP / ideal approach + stair',18,C.ink,'left',true);m.carrier.filter(r=>r.up!=='LIFT').forEach((r,i)=>text(x,1348+i*30,r.up+' / '+r.approachMetres.toFixed(1)+'m + '+(r.up==='F'?m.stats.featureFlight3D:m.stats.escapeStair3D).toFixed(1)+'m',18,C.muted));
 }else{
 text(x,960,'OFFICE ADDRESS / PLAY PURPOSE',18,C.ink,'left',true);['N1 Lift Lobby / court straight ahead','N2 Court / shared case + feature stair','N3 Kitchen / west ring junction','N4 Coffee / east ring junction','N5 Legal Entry / N6 Review Entry','K1 Lift lightbox / K2 stair + bridge','K3 / K4 firm reception lightboxes'].forEach((s,i)=>text(x,997+i*32,s,18,C.muted));
 text(x,1260,'CORE FACES ARE INHABITED',18,C.ink,'left',true);['Print + kitchen / server + coffee','Two-door washrooms; 2 x 2m risers','Only risers + lift shaft stay sealed','Desks face daylight at the perimeter'].forEach((s,i)=>text(x,1300+i*33,s,18,C.muted));
 }
 text(x,1480,'Gates shown as UPPER arrival traces.',17,'#C0281E');text(x,1510,'Lower floor is open; void exists above.',17,C.muted);text(x,1540,'Lift reserved; availability undecided.',17,C.muted);text(x,1570,'*Checkpoint bridge is conditional.',17,C.muted);text(x,1610,'Glass bridge deck is NOT overhead cover.',17,C.muted);text(x,1670,'CONCEPT / NOT IMPLEMENTED',18,C.blue,'left',true);
};
footer=function(m){const y=OY+m.h*PPM+70;for(let i=0;i<8;i++)rect(OX+i*40,y,40,14,i%2?C.white:C.ink,C.ink,1);text(OX,y+33,'0',16);text(OX+160,y+33,'4',16);text(OX+320,y+33,'8m',16);text(OX+420,y+9,'40 px/m / 4m grid / NW origin / plan y = Unity -Z',18,C.muted);text(OX,y+72,'Doors 1.2 x 2.4m / floor rise 4.5m / slab 0.2m / soffit 4.3m / cage continuous above ramp.',18,C.muted);text(OX,y+104,'Ring = pale blue; loops = blue dash; B = shared case; gates = red dash, not decorative glass.',18,C.muted);};
// Correct the reused section to the offset stair; registered plans remain exact.
const l1Stack=stack;
stack=function(m){l1Stack(m);
 // Replace the output with additional measured progression and bridge notes.
 rect(1380,1440,1280,335,'#FFFFFF');
 text(1420,1475,'G1 / G2 / GF at every upper arrival, including roof.',21,C.ink,'left',true);
 text(1420,1515,'F: 2 x 7.2m ramp / rise 4.5m / path 8.49m.',20,C.muted);
 text(1420,1555,'E1/E2: runs + return + two 2m end landings = 16.09m.',20,C.muted);
 text(1420,1595,'North bridge: 8 x 2.4m at +4.5 / +9m only.',20,C.blue);
 text(1420,1635,'South canopy: 4 x 2m / slab +4.5m / clear 4.3m.',20,C.muted);
 text(1420,1675,'Clear bridge / guard is not cover; solid canopy is.',20,C.muted);
 text(1420,1715,'Fixed roof cages stop mantle/jump bypass of gates.',20,'#C0281E');
 // Draw bridges and arrival gates on the registered upper mini-plans.
 for(let f=1;f<4;f++){const y=310+f*345;for(const g of m.gates)line([165+g.a[0]*10,y+g.a[1]*10],[165+g.b[0]*10,y+g.b[1]*10],'#C0281E',4,true);if(f<3)rect(325,y+100,80,24,'none',C.electric,2);}
 text(1420,375,'Longitudinal section, east to west; slab gap = 7.2m.',18,C.muted);
 for(let f=1;f<4;f++){const y=1370-f*4.5*42;line([1470+15.6*42,y],[1470+15.6*42,y-4.3*42],'#C0281E',3,true);}
 save(m,'stack');
};
for(const m of models){
 for(const kind of ['plan','flow']){D=[];header(m,kind==='plan'?'PLAN / measured lower floor + upper traces':'FLOW / ring, routes, gates and sightlines');shell(m);dimension(m);footer(m);sidebar(m,kind);if(kind==='flow'){
  flow(m); // Retain L1 overlay semantics, omit its obsolete lift-route footer.
  D=D.filter(d=>!(d.type==='text'&&d.value.startsWith('Dashed arrows:')));
  text(140,1820,'Dashed arrows: F exposed / E1-E2 enclosed / L reserved shaft only. Time estimates exclude combat and turning.',17,C.muted);
 }save(m,kind);}
 stack(m);
}
const m=models[0];
const metrics={id:m.id,...m.stats,sightlines:m.sightlines,
 lanes:m.lanes.map(r=>({name:r.name,metres:len(r.p),secondsAt9:len(r.p)/9})),
 caseApproaches:m.caseApproaches.map(r=>({name:r.name,metres:len(r.p),secondsAt9:len(r.p)/9})),
 rotations:m.rotations.map(r=>({name:r.name,metres:len(r.p),secondsAt9:len(r.p)/9})),
 carrier:m.carrier.map(r=>({...r,totalPathMetres:r.approachMetres+(r.up==='F'?m.stats.featureFlight3D:r.up==='LIFT'?0:m.stats.escapeStair3D),secondsAt9:r.up==='LIFT'?null:(r.approachMetres+(r.up==='F'?m.stats.featureFlight3D:m.stats.escapeStair3D))/9}))};
fs.writeFileSync(path.join(ROOT,'L2/layouts.json'),JSON.stringify({units:'metres',origin:'NW; x east, y south = Unity -Z',ppm:40,models},null,2));
fs.writeFileSync(path.join(ROOT,'L2/metrics.json'),JSON.stringify(metrics,null,2));
console.log(JSON.stringify(metrics,null,2));
`;
const inputs=['draw_L1.mjs','render_L1.ps1','layouts_L1.json','metrics_L1.json'];
const hashes=()=>Object.fromEntries(inputs.map(f=>[f,crypto.createHash('sha256').update(fs.readFileSync(path.join(root,f))).digest('hex')]));
const before=hashes();
vm.runInNewContext(`const ROOT=ROOTPARAM;const surveyKey=m=>JSON.stringify([m.w,m.h,m.walls,m.services,m.feature]);${shared}\n${setupL2.toString()}\nconst models=[setupL2()];\n${measurement}\n${drawing}\n${custom}`,{fs,path,console,ROOTPARAM:root,CACHED:cached});
fs.writeFileSync(path.join(out,'source_provenance.json'),JSON.stringify({l1InputSha256:before,unchanged:JSON.stringify(before)===JSON.stringify(hashes())},null,2));

