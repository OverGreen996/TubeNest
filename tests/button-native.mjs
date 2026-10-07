import {chromium} from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
const out=path.resolve('test-output/button-native');fs.mkdirSync(out,{recursive:true});
const extension=path.resolve('extension');
const ctx=await chromium.launchPersistentContext(path.join(out,'profile-'+Date.now()),{channel:'msedge',headless:true,args:[`--disable-extensions-except=${extension}`,`--load-extension=${extension}`]});
try{
 const worker=ctx.serviceWorkers()[0]||await ctx.waitForEvent('serviceworker');
 await worker.evaluate(()=>chrome.runtime.onMessage.addListener((m,s)=>{if(m.action==='open')globalThis.lastButtonSender={url:s.url,origin:s.origin,frameId:s.frameId,tabId:s.tab?.id,messageUrl:m.url};}));
 await ctx.route('https://www.youtube.com/**',r=>r.request().isNavigationRequest()?r.fulfill({contentType:'text/html; charset=utf-8',body:'<!doctype html><html><head><style>body{background:#0f0f0f;color:#f1f1f1}#top-level-buttons-computed{display:flex;align-items:center}#top-level-buttons-computed>span{margin:0}button{height:36px}#actions{display:flex}</style></head><body><ytd-watch-flexy><div id="actions"><div id="top-level-buttons-computed"><span><button aria-label="分享">分享</button></span></div><button aria-label="儲存">儲存</button></div></ytd-watch-flexy></body></html>'}):r.continue());
 const page=await ctx.newPage();await page.goto('https://www.youtube.com/');
 // YouTube normally opens a video without a new document, so sender.url remains the homepage.
 await page.evaluate(()=>{history.pushState({},'','/watch?v=yRRQ6filg-E');document.documentElement.setAttribute('dark','');document.dispatchEvent(new Event('yt-navigate-finish'));});
 const button=page.locator('#tubenest-download button');await button.waitFor();await button.click();
 const sender=await worker.evaluate(()=>globalThis.lastButtonSender);
 assert.equal(sender.url,'https://www.youtube.com/');assert.equal(sender.frameId,0);
 if(process.argv.includes('--diagnose')){
  await page.waitForFunction(()=>document.querySelector('#tubenest-download')?.shadowRoot?.querySelector('button').title!=='TubeNest 下載影片');
  const result={sender,title:await button.getAttribute('title'),spacing:await page.locator('#tubenest-download').evaluate(e=>getComputedStyle(e).margin)};
  fs.writeFileSync(path.join(out,'before.json'),JSON.stringify(result,null,2));console.log('REPRODUCED',result);
 }else{
  assert.equal(sender.messageUrl,'https://www.youtube.com/watch?v=yRRQ6filg-E');
  const spacing=await page.locator('#tubenest-download').evaluate(e=>({left:getComputedStyle(e).marginLeft,right:getComputedStyle(e).marginRight}));assert.deepEqual(spacing,{left:'8px',right:'8px'});
  const cdp=await ctx.browser().newBrowserCDPSession();let target;
  for(let i=0;i<40&&!target;i++){target=(await cdp.send('Target.getTargets')).targetInfos.find(t=>t.url.endsWith('/popup.html'));if(!target)await new Promise(r=>setTimeout(r,100));}assert.ok(target,'Clicking the page button must open the real extension popup');
  const {sessionId}=await cdp.send('Target.attachToTarget',{targetId:target.targetId,flatten:false});let sequence=0;const pending=new Map();
  cdp.on('Target.receivedMessageFromTarget',event=>{const r=JSON.parse(event.message);if(r.id){pending.get(r.id)?.(r);pending.delete(r.id);}});
  const evaluate=expression=>new Promise((resolve,reject)=>{const id=++sequence;pending.set(id,r=>r.error||r.result.exceptionDetails?reject(new Error(JSON.stringify(r))):resolve(r.result.result.value));cdp.send('Target.sendMessageToTarget',{sessionId,message:JSON.stringify({id,method:'Runtime.evaluate',params:{expression,returnByValue:true,awaitPromise:true,userGesture:true}})}).catch(reject);});
  let result;
  for(let i=0;i<180;i++){result=await evaluate(`({ready:!document.querySelector('#settings').hidden,title:document.querySelector('#title').textContent,error:document.querySelector('#message').classList.contains('error')?document.querySelector('#message').textContent:'',options:[...document.querySelector('#quality').options].map(o=>o.textContent)})`);if(result.error)throw new Error(result.error);if(result.ready)break;await new Promise(r=>setTimeout(r,500));}
  assert.equal(result.ready,true);assert.match(result.title,/許純美/);assert.ok(result.options.includes('1080p'));fs.writeFileSync(path.join(out,'after.json'),JSON.stringify({sender,spacing,...result},null,2));
  console.log('PASS: homepage → SPA video → trusted page-button click → real Edge popup → real YouTube qualities, with both side margins.');
 }
}finally{await ctx.close();}
