(()=>{
 let host,row,timer;const page=()=>location.pathname==='/watch'&&/^[\w-]{11}$/.test(new URL(location.href).searchParams.get('v')||'')||/^\/shorts\/[\w-]{11}\/?$/.test(location.pathname);
 const theme=new MutationObserver(()=>host?.toggleAttribute('dark',document.documentElement.hasAttribute('dark')));
 const discovery=new MutationObserver(schedule),local=new MutationObserver(schedule);
 function schedule(){if(!timer)timer=setTimeout(()=>{timer=null;mount();},200);}
 function visible(el){const r=el.getBoundingClientRect();return r.height>0&&r.width>0&&r.bottom>0&&r.top<innerHeight;}
 function create(){host=document.createElement('span');host.id='tubenest-download';const shadow=host.attachShadow({mode:'open'});
 shadow.innerHTML=`<style>
 :host{display:inline-flex!important;flex:0 0 auto!important;margin:0 8px!important;position:relative;vertical-align:middle;--md-bg:#f2f2f2;--md-hover:#e5e5e5;--md-text:#0f0f0f;--md-focus:#065fd4}
 :host([dark]){--md-bg:#272727;--md-hover:#3f3f3f;--md-text:#f1f1f1;--md-focus:#3ea6ff}
 button{display:inline-flex;align-items:center;justify-content:center;gap:8px;min-height:36px;padding:0 16px;border:0;border-radius:999px;background:var(--md-bg);color:var(--md-text);font:500 14px/1.2 Roboto,"Segoe UI",sans-serif;cursor:pointer;white-space:nowrap}button:disabled{cursor:wait;opacity:.7}
 button{transition:background-color 140ms ease}button:hover{background:var(--md-hover)}button:active:not(:disabled){filter:brightness(.92)}button:focus-visible{outline:2px solid var(--md-focus);outline-offset:3px}svg{width:20px;height:20px;fill:none;stroke:currentColor;stroke-width:1.8;stroke-linecap:round;stroke-linejoin:round}
 :host([shorts]){margin:8px 0!important;display:flex;justify-content:center}:host([shorts]) button{flex-direction:column;gap:6px;background:transparent;padding:8px 4px;font-size:12px}:host([shorts]) svg{box-sizing:content-box;padding:12px;border-radius:50%;width:24px;height:24px;background:var(--md-bg)}:host([shorts]) button:hover svg{background:var(--md-hover)}
 .error{position:absolute;top:calc(100% + 8px);right:0;z-index:10000;width:max-content;max-width:280px;padding:10px 12px;border-radius:8px;background:var(--md-bg);color:var(--md-text);box-shadow:0 2px 12px #0004;font:14px/1.5 "Segoe UI",sans-serif;white-space:normal}.error[hidden]{display:none}
 @media(max-width:650px){:host(:not([shorts])) button{padding:0;width:36px}:host(:not([shorts])) button span{display:none}}
 @media(prefers-reduced-motion:reduce){button{transition:none}}
 </style><button type="button" title="TubeNest 下載影片" aria-label="使用 TubeNest 下載影片"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3v12m-5-5 5 5 5-5M4 16v4h16v-4"/></svg><span>下載</span></button><span class="error" role="status" aria-live="polite" hidden></span>`;
 const button=shadow.querySelector('button'),label=button.querySelector('span'),error=shadow.querySelector('.error');
 button.onclick=async event=>{if(!event.isTrusted)return;event.preventDefault();event.stopPropagation();button.disabled=true;label.textContent='開啟中';error.hidden=true;try{const reply=await chrome.runtime.sendMessage({action:'open',url:location.href});if(!reply?.ok)throw new Error(reply?.error||'請點工具列 TubeNest');button.title='TubeNest 下載影片';}catch(e){const text=/context invalidated/i.test(e.message)?'擴充已更新，請重新整理 YouTube 後再點下載。':e.message;button.title=text;error.textContent=text;error.hidden=false;}finally{button.disabled=false;label.textContent='下載';}};
 }
 function mount(){if(!page()){cleanup();return;}const next=location.pathname==='/watch'?document.querySelector('ytd-watch-flexy #top-level-buttons-computed'):[...document.querySelectorAll('ytd-reel-video-renderer[is-active] #actions, ytd-shorts reel-action-bar-view-model')].find(visible);
 if(!next){host?.remove();row=null;local.disconnect();if(document.body)discovery.observe(document.body,{childList:true,subtree:true});return;}
 if(!host)create();host.toggleAttribute('dark',document.documentElement.hasAttribute('dark'));host.toggleAttribute('shorts',location.pathname.startsWith('/shorts/'));
 const share=[...next.querySelectorAll('button')].find(b=>/分享|share|共有|공유/i.test(b.getAttribute('aria-label')||b.textContent));let anchor=share;while(anchor&&anchor.parentElement!==next)anchor=anchor.parentElement;if(anchor&&host.previousElementSibling!==anchor)anchor.after(host);else if(!anchor&&host.parentElement!==next)next.append(host);
 discovery.disconnect();if(row!==next){row=next;local.disconnect();local.observe(row,{childList:true,subtree:true});for(let p=row.parentElement;p&&p!==document.body;p=p.parentElement)local.observe(p,{childList:true});}
 }
 function cleanup(){clearTimeout(timer);timer=null;discovery.disconnect();local.disconnect();theme.disconnect();host?.remove();row=null;}
 function begin(){cleanup();if(page()){theme.observe(document.documentElement,{attributes:true,attributeFilter:['dark']});mount();}}
 document.addEventListener('yt-navigate-start',cleanup);document.addEventListener('yt-navigate-finish',begin);window.addEventListener('popstate',begin);window.addEventListener('pagehide',cleanup);begin();
})();
