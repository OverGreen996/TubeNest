import {chromium} from 'playwright';import fs from 'node:fs';import path from 'node:path';import assert from 'node:assert/strict';import {execFileSync} from 'node:child_process';
const extension=path.resolve(process.env.LOCALAPPDATA,'TubeNestYouTube/extension'),out=path.resolve('test-output/rebuild-edge');fs.mkdirSync(out,{recursive:true});
const ctx=await chromium.launchPersistentContext(path.join(out,'profile-'+Date.now()),{channel:'msedge',headless:true,args:[`--disable-extensions-except=${extension}`,`--load-extension=${extension}`]});
try{
 const worker=ctx.serviceWorkers()[0]||await ctx.waitForEvent('serviceworker');assert.equal(new URL(worker.url()).host,'fhjcedbbonnlkilcklhodjejolannode');
 await ctx.route('https://www.youtube.com/watch?**',r=>r.request().isNavigationRequest()?r.fulfill({contentType:'text/html; charset=utf-8',body:'<html><meta charset="utf-8"><title>三立新聞 · YouTube</title><body>Native integration test</body></html>'}):r.continue());
 const page=await ctx.newPage();await page.goto('https://www.youtube.com/watch?v=yRRQ6filg-E');await worker.evaluate(()=>chrome.action.openPopup());
 const cdp=await ctx.browser().newBrowserCDPSession();const target=(await cdp.send('Target.getTargets')).targetInfos.find(t=>t.url.endsWith('/popup.html'));assert.ok(target);
 const {sessionId}=await cdp.send('Target.attachToTarget',{targetId:target.targetId,flatten:false});let seq=0;const pending=new Map();
 cdp.on('Target.receivedMessageFromTarget',event=>{const r=JSON.parse(event.message);if(r.id){pending.get(r.id)?.(r);pending.delete(r.id);}});
 const evaluate=expression=>new Promise(resolve=>{const id=++seq;pending.set(id,r=>{if(r.result.exceptionDetails)throw new Error(JSON.stringify(r.result.exceptionDetails));resolve(r.result.result.value);});cdp.send('Target.sendMessageToTarget',{sessionId,message:JSON.stringify({id,method:'Runtime.evaluate',params:{expression,returnByValue:true,awaitPromise:true,userGesture:true}})});});
 for(let n=0;n<50;n++){if(await evaluate(`!document.querySelector('#probe').disabled`))break;await new Promise(r=>setTimeout(r,100));}
 const count=()=>Number(execFileSync('powershell.exe',['-NoProfile','-Command',"@(Get-Process TubeNestHost -ErrorAction SilentlyContinue).Count"],{encoding:'utf8',windowsHide:true}).trim());
 // Opening the popup is the user's download-button action; it reads qualities once.
 for(let n=0;n<180;n++){if(await evaluate(`!document.querySelector('#settings').hidden`))break;const error=await evaluate(`document.querySelector('#message').classList.contains('error')?document.querySelector('#message').textContent:''`);if(error)throw new Error(error);await new Promise(r=>setTimeout(r,500));}
 const result=await evaluate(`({title:document.querySelector('#title').textContent,quality:document.querySelector('#quality').value,options:[...document.querySelector('#quality').options].map(o=>o.textContent)})`);assert.match(result.title,/許純美/);assert.equal(result.quality,'1080');
 for(let n=0;n<20&&count();n++)await new Promise(r=>setTimeout(r,200));assert.equal(count(),0,'Completed metadata probe must leave no native helper');
 fs.writeFileSync(path.join(out,'native-integration.json'),JSON.stringify({...result,idleHelpers:0,extensionVersion:await worker.evaluate(()=>chrome.runtime.getManifest().version)},null,2));console.log('PASS: actual Edge popup → registered hidden native host → real YouTube quality data → no native helper remains.');
}finally{await ctx.close();}
