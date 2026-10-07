import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {spawn,execFileSync} from 'node:child_process';
import {randomUUID} from 'node:crypto';
const root=path.resolve('test-output/rebuild-runtime/native'),out=path.resolve('test-output/rebuild-verified');fs.mkdirSync(out,{recursive:true});
const origin='chrome-extension://fhjcedbbonnlkilcklhodjejolannode/';
function frames(proc){let bytes=Buffer.alloc(0);const messages=[],waiters=[];proc.stdout.on('data',data=>{bytes=Buffer.concat([bytes,data]);while(bytes.length>=4&&bytes.length>=4+bytes.readUInt32LE(0)){const n=bytes.readUInt32LE(0),message=JSON.parse(bytes.subarray(4,n+4).toString());bytes=bytes.subarray(n+4);messages.push(message);const w=waiters.find(w=>w.id===message.id);if(w){waiters.splice(waiters.indexOf(w),1);clearTimeout(w.timer);w.resolve(message);}}});return {messages,rpc(action,data={}){const id=randomUUID();return new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(new Error('RPC timeout')),180000);waiters.push({id,resolve,timer});const body=Buffer.from(JSON.stringify({id,action,...data})),head=Buffer.alloc(4);head.writeUInt32LE(body.length);proc.stdin.write(Buffer.concat([head,body]));});}};}
const host=spawn(path.join(root,'TubeNestHost.exe'),[origin],{windowsHide:true});const channel=frames(host);
try{
 for(const url of ['http://127.0.0.1/a','https://youtube.com.evil.com/watch?v=yRRQ6filg-E','https://www.youtube.com/watch?v=bad','file:///C:/x'])assert.equal((await channel.rpc('probe',{url})).ok,false);
 const info=await channel.rpc('probe',{url:'https://www.youtube.com/watch?v=yRRQ6filg-E'});assert.equal(info.ok,true,info.error);assert.equal(info.data.id,'yRRQ6filg-E');assert.ok(info.data.qualities.some(q=>q.height===1080));
 const bad=await channel.rpc('start',{url:'https://www.youtube.com/watch?v=yRRQ6filg-E',quality:'1080',jobId:randomUUID(),expected:{id:'mqK1eU5k2CQ',duration:103}});assert.equal(bad.ok,false);
 host.stdin.end();const exit=await new Promise(resolve=>host.once('exit',resolve));assert.equal(exit,0);assert.equal(host.exitCode,0);
 console.log('PASS: native framing, URL/identity rejection, live quality probe, process exits on port EOF.');
}finally{if(host.exitCode===null)host.kill();}
if(process.argv.includes('--live')){
 for(const [name,url,height] of [['news','https://www.youtube.com/watch?v=yRRQ6filg-E',1080],['home','https://www.youtube.com/watch?v=mqK1eU5k2CQ',1080],['shorts','https://www.youtube.com/shorts/18NGQq7p3LY',2964]]){
  const file=path.join(out,name+'.mp4');if(fs.existsSync(file))throw new Error('Test destination already exists');
  const p=spawn(path.resolve('test-output/rebuild-runtime/native/NativeHarness.exe'),[path.join(root,'TubeNestHost.exe'),'download',file,url,String(height)],{windowsHide:true});const state=frames(p);p.stderr.on('data',d=>process.stderr.write(d));
  const code=await new Promise(resolve=>p.once('exit',resolve));assert.equal(code,0,JSON.stringify(state.messages.at(-1)));const last=state.messages.at(-1)?.job;assert.equal(last.status,'completed',last.error);assert.equal(last.verification.height,height);assert.equal(last.verification.sourceId,url.split(/[=/]/).at(-1));assert.ok(last.verification.fullDecode);assert.ok(last.verification.frames>0);
  fs.writeFileSync(path.join(out,name+'-report.json'),JSON.stringify(last,null,2));console.log('PASS live',name,last.verification);
 }
 const truncated=path.join(out,'truncated.mp4');const bytes=fs.readFileSync(path.join(out,'news.mp4'));fs.writeFileSync(truncated,bytes.subarray(0,Math.floor(bytes.length*.6)));
 const r=spawn(path.resolve('test-output/rebuild-runtime/native/NativeHarness.exe'),[path.join(root,'TubeNestHost.exe'),'verify',truncated,'https://www.youtube.com/watch?v=yRRQ6filg-E','1080','103'],{windowsHide:true});r.stdout.resume();r.stderr.resume();assert.equal(await new Promise(resolve=>r.once('exit',resolve)),3);console.log('PASS: truncated file rejected by production verifier.');
}
if(process.argv.includes('--cancel')){
 const file=path.join(out,'cancelled.mp4');assert.equal(fs.existsSync(file),false);
 const p=spawn(path.resolve('test-output/rebuild-runtime/native/NativeHarness.exe'),[path.join(root,'TubeNestHost.exe'),'cancel',file,'https://www.youtube.com/watch?v=mqK1eU5k2CQ','1080'],{windowsHide:true});const stream=frames(p);p.stderr.on('data',d=>process.stderr.write(d));assert.equal(await new Promise(resolve=>p.once('exit',resolve)),0);assert.ok(stream.messages.some(m=>m.job?.status==='cancelled'));assert.equal(fs.existsSync(file),false);assert.equal(fs.readdirSync(out).some(n=>n.startsWith('.TubeNest-')),false);console.log('PASS: actual download cancellation, no saved file, temporary data cleaned.');
}
