import {videoId,videoUrl} from './youtube.js';
const HOST='com.tubenest.youtube',terminal=new Set(['completed','failed','cancelled']);
let port,saveQueue=Promise.resolve();
const requests=new Map(),jobs=new Set(),probes=new Map();
async function update(patch){saveQueue=saveQueue.catch(()=>{}).then(async()=>{const {history=[]}=await chrome.storage.local.get('history');const index=history.findIndex(j=>j.id===patch.id);if(index>=0&&terminal.has(history[index].status))return;if(index<0)history.unshift({...patch,created:Date.now()});else history[index]={...history[index],...patch};await chrome.storage.local.set({history:history.slice(0,30)});});return saveQueue;}
function closeIdle(){if(!requests.size&&!jobs.size&&port){const prior=port;port=null;prior.disconnect();}}
function connect(){
 if(port)return port;
 const native=chrome.runtime.connectNative(HOST);port=native;
 native.onMessage.addListener(message=>{
   if(message.event==='job'&&jobs.has(message.job?.id)){
     const job=message.job;update(job);
     if(terminal.has(job.status)){jobs.delete(job.id);closeIdle();}return;
   }
   const request=requests.get(message.id);if(!request)return;
   requests.delete(message.id);clearTimeout(request.timer);
   if(message.ok)request.resolve(message.data);else request.reject(new Error(message.error||'本機助手處理失敗。'));
   closeIdle();
 });
 native.onDisconnect.addListener(()=>{
   const reason=chrome.runtime.lastError?.message||'本機助手連線已中斷。';if(port!==native)return;port=null;
   for(const r of requests.values()){clearTimeout(r.timer);r.reject(new Error(/not found|not registered|not installed/i.test(reason)?'請先執行安裝本機助手，再重開面板。':reason));}requests.clear();
   for(const id of jobs)update({id,status:'failed',error:'本機助手已中斷，請重新下載。'});jobs.clear();
 });return native;
}
function rpc(action,data={}){return new Promise((resolve,reject)=>{const id=crypto.randomUUID();const timer=setTimeout(()=>{requests.delete(id);reject(new Error('本機助手回應逾時。'));if(data.jobId){jobs.delete(data.jobId);update({id:data.jobId,status:'failed',error:'工作未能啟動，請重試。'});}closeIdle();},action==='start'?600000:180000);requests.set(id,{resolve,reject,timer});try{connect().postMessage({id,action,...data});}catch(error){clearTimeout(timer);requests.delete(id);reject(error);closeIdle();}});}
async function current(){const [tab]=await chrome.tabs.query({active:true,currentWindow:true});if(!tab?.id||!videoId(tab.url))throw new Error('請先開啟 YouTube 影片或 Shorts。');return tab;}
async function same(url,tabId){const tab=await current();if(tab.id!==tabId||videoUrl(tab.url)!==url)throw new Error('目前影片已切換，請重新讀取。');return tab;}
async function handle(m){
 if(m.action==='page'){const [tab]=await chrome.tabs.query({active:true,currentWindow:true});return {tabId:tab?.id,url:videoUrl(tab?.url),title:(tab?.title||'YouTube 影片').replace(/\s*[-–]\s*YouTube$/,'')};}
 if(m.action==='probe'){
   await same(m.url,m.tabId);const prior=probes.get(m.url);if(m.reuse&&prior&&Date.now()-prior.time<300000&&prior.tabId===m.tabId)return prior.info;
   const info=await rpc('probe',{url:m.url});await same(m.url,m.tabId);
   if(info.id!==videoId(m.url)||!info.qualities?.length)throw new Error('未取得目前影片的可用畫質。');
   probes.set(m.url,{info,time:Date.now(),tabId:m.tabId});if(probes.size>8)probes.delete(probes.keys().next().value);return info;
 }
 if(m.action==='start'){
   if(jobs.size)throw new Error('請等目前下載完成，或先取消。');await same(m.url,m.tabId);
   const cached=probes.get(m.url);if(!cached||Date.now()-cached.time>300000||cached.tabId!==m.tabId)throw new Error('影片資訊已過期，請重新讀取畫質。');
   if(!cached.info.qualities.some(q=>String(q.height)===m.quality))throw new Error('請選擇影片實際提供的畫質。');
   const id=crypto.randomUUID();jobs.add(id);await update({id,url:m.url,title:cached.info.title,status:'choosing',quality:m.quality});
   try{const result=await rpc('start',{jobId:id,url:m.url,quality:m.quality,expected:cached.info});if(result.cancelled){jobs.delete(id);await update({id,status:'cancelled'});closeIdle();}return result;}
   catch(error){jobs.delete(id);await update({id,status:'failed',error:error.message});closeIdle();throw error;}
 }
 if(m.action==='cancel'){if(jobs.has(m.id))await rpc('cancel',{jobId:m.id});return {};}
 if(m.action==='reveal'){const {history=[]}=await chrome.storage.local.get('history');const job=history.find(j=>j.id===m.id&&j.status==='completed');if(!job?.path)throw new Error('找不到已完成檔案。');return rpc('reveal',{path:job.path});}
 if(m.action==='clear'){await saveQueue;const {history=[]}=await chrome.storage.local.get('history');await chrome.storage.local.set({history:history.filter(j=>!terminal.has(j.status))});return {};}
 throw new Error('不支援的操作。');
}
chrome.runtime.onMessage.addListener((m,s,reply)=>{
 if(m.action==='open'){
   (async()=>{let origin;try{origin=new URL(s.url).origin;}catch{}if(s.id!==chrome.runtime.id||s.frameId!==0||!s.tab?.id||origin!=='https://www.youtube.com'||!videoId(m.url))throw new Error('請在目前影片點下載。');const [tab]=await chrome.tabs.query({active:true,windowId:s.tab.windowId});if(tab?.id!==s.tab.id||videoId(tab.url)!==videoId(m.url))throw new Error('影片已切換，請重新點下載。');await chrome.action.openPopup({windowId:tab.windowId});return {};})().then(data=>reply({ok:true,data}),e=>reply({ok:false,error:e.message}));return true;
 }
 if(s.id!==chrome.runtime.id||s.tab||s.url!==chrome.runtime.getURL('popup.html')){reply({ok:false,error:'未授權的來源。'});return false;}
 handle(m).then(data=>reply({ok:true,data}),e=>reply({ok:false,error:e.message}));return true;
});
chrome.storage.local.get('history').then(({history=[]})=>{for(const j of history)if(!terminal.has(j.status)){j.status='failed';j.error='瀏覽器或擴充已重啟，工作已中止。';}return chrome.storage.local.set({history});});
