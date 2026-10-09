import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import {launchOptions, outputDirectory, testBrowser} from './test-browser.mjs';
const root = path.resolve('extension'), out = outputDirectory('ui-states', 'test-output/ui-polish');
fs.mkdirSync(out, {recursive:true});
const server = http.createServer((req, res) => {
  const file = path.resolve(root, req.url.slice(1) || 'popup.html');
  if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) return res.writeHead(404).end();
  res.setHeader('Content-Type', file.endsWith('.js') ? 'text/javascript' : file.endsWith('.css') ? 'text/css' : 'text/html');
  res.end(fs.readFileSync(file));
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const url = 'http://127.0.0.1:' + server.address().port;
const browser = await chromium.launch(launchOptions);
fs.writeFileSync(path.join(out,'browser.json'), JSON.stringify({browser:testBrowser,version:browser.version()},null,2));
const errors = [];
const fixtures = {
  title: '山與海之間，留一段旅途給自己｜週末影像日記',
  uploader: '週末影像日記', duration:487,
  qualities:[{height:2160,width:3840},{height:1440,width:2560},{height:1080,width:1920},{height:720,width:1280}]
};
async function open(scheme, width = 384, mode = 'ready') {
  const context = await browser.newContext({viewport:{width,height:600}, colorScheme:scheme, reducedMotion:'reduce'});
  await context.addInitScript(({fixtures, mode}) => {
    window.calls = [];
    window.probeMode = mode;
    window.chrome = {
      runtime:{
        openOptionsPage() { calls.push({action:'help'}); },
        async sendMessage(m) {
          calls.push(m);
          if (m.action === 'page') return {ok:true,data: mode === 'unsupported' ? {title:'',url:''} : {title:fixtures.title,url:'https://www.youtube.com/watch?v=yRRQ6filg-E',tabId:1}};
          if (m.action === 'probe') {
            if (window.probeMode === 'loading') await new Promise(resolve => window.finishProbe = resolve);
            if (window.probeMode === 'failed') return {ok:false,error:'來源回應 403，請更新下載工具後再試一次。'};
            return {ok:true,data: fixtures};
          }
          return {ok:true,data:{cancelled:true}};
        }
      },
      storage:{
        local:{get:async()=>({history:[]})},
        onChanged:{addListener(callback) {window.emitHistory = history => callback({history:{newValue:history}}, 'local');}}
      }
    };
  }, {fixtures, mode});
  const page = await context.newPage();
  page.on('pageerror', e => errors.push(e.message));
  await page.goto(url + '/popup.html');
  await page.waitForFunction(() => typeof window.emitHistory === 'function');
  return {context, page};
}
async function fits(page) {
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'No horizontal overflow');
}
function luminance(rgb) {
  const channels = rgb.match(/\d+/g).slice(0,3).map(Number).map(n => n/255).map(n => n <= .04045 ? n/12.92 : ((n+.055)/1.055)**2.4);
  return .2126*channels[0] + .7152*channels[1] + .0722*channels[2];
}
try {
  for (const scheme of ['light', 'dark']) {
    const {context, page} = await open(scheme);
    await page.locator('#settings:not([hidden])').waitFor();
    await fits(page);
    assert.equal(await page.locator('#quality').inputValue(), '1080');
    assert.equal(await page.evaluate(() => document.body.scrollHeight <= 600), true, 'Ready popup fits Chrome/Edge height');
    for (const pair of [['--text','--bg'],['--muted','--bg'],['--on-accent','--accent'],['--error','--error-bg'],['--success','--surface']]) {
      const colors = await page.evaluate(([fg, bg]) => {
        const s = getComputedStyle(document.documentElement), el = document.createElement('i');
        document.body.append(el);
        el.style.color = s.getPropertyValue(fg); const a = getComputedStyle(el).color;
        el.style.color = s.getPropertyValue(bg); const b = getComputedStyle(el).color;
        el.remove(); return [a,b];
      }, pair);
      const values = colors.map(luminance);
      assert.ok((Math.max(...values)+.05)/(Math.min(...values)+.05) >= 4.5, scheme + ' contrast ' + pair);
    }
    await page.screenshot({path:path.join(out, 'popup-'+scheme+'.png'), clip:await page.locator('body').boundingBox()});
    const layout = await page.evaluate(() => Object.fromEntries(['body','.brand','#title','#detail','#settings','#quality','#download','summary','footer'].map(selector => {
      const e = document.querySelector(selector), rect = e.getBoundingClientRect(), style = getComputedStyle(e);
      return [selector,{x:rect.x,y:rect.y,width:rect.width,height:rect.height,color:style.color,background:style.backgroundColor,font:style.fontFamily,fontSize:style.fontSize,borderRadius:style.borderRadius}];
    })));
    fs.writeFileSync(path.join(out,'layout-'+scheme+'.json'), JSON.stringify(layout,null,2));
    await page.locator('#quality').selectOption('2160');
    await page.locator('#probe').click();
    await page.locator('#probe:not([disabled])').waitFor();
    assert.equal(await page.locator('#quality').inputValue(), '2160', 'Refresh preserves selected quality');
    await page.locator('#download').click();
    await page.getByText('已取消。', {exact:true}).waitFor();
    assert.equal(await page.evaluate(() => calls.findLast(m => m.action === 'start').quality), '2160');
    await page.getByText('還沒有下載紀錄', {exact:true}).waitFor();
    assert.equal(await page.locator('#clear').isVisible(), false);
    await page.evaluate(() => emitHistory([{id:'job',title:'山與海之間，留一段旅途給自己',status:'downloading',progress:47}]));
    assert.equal(await page.locator('#download').isDisabled(), true, 'Prevent a second job while working');
    assert.match(await page.locator('#message').textContent(), /下載中/);
    assert.equal(await page.locator('progress').getAttribute('value'), '47');
    await page.screenshot({path:path.join(out,'progress-'+scheme+'.png'), fullPage:true});
    await page.getByRole('button',{name:'取消下載',exact:true}).click();
    assert.equal(await page.evaluate(() => calls.findLast(m => m.action === 'cancel').id), 'job');
    await page.evaluate(() => emitHistory([{id:'job',title:'山與海之間，留一段旅途給自己',status:'verifying',progress:100}]));
    assert.equal(await page.locator('progress').getAttribute('value'), null, 'Verification must not claim download percentage');
    await page.evaluate(() => emitHistory([{id:'job',title:'山與海之間，留一段旅途給自己',status:'completed',progress:100}]));
    assert.equal(await page.locator('#download').isDisabled(), false);
    assert.match(await page.locator('#message').textContent(), /檔案已儲存/);
    await page.getByRole('button',{name:'在資料夾中顯示',exact:true}).click();
    assert.equal(await page.evaluate(() => calls.findLast(m => m.action === 'reveal').id), 'job');
    await page.screenshot({path:path.join(out,'completed-'+scheme+'.png'), fullPage:true});
    await page.getByRole('button',{name:'清除已結束紀錄'}).click();
    assert.equal(await page.evaluate(() => calls.at(-1).action), 'clear');
    await page.locator('#help').click();
    assert.equal(await page.evaluate(() => calls.at(-1).action), 'help');
    await page.evaluate(() => {window.probeMode = 'failed';});
    await page.locator('#probe').click();
    await page.locator('#message.error').waitFor();
    assert.equal(await page.locator('#settings').isVisible(), false, 'Do not offer stale qualities after failed refresh');
    assert.equal(await page.locator('#probe').isDisabled(), false, 'Retry stays available');
    await page.screenshot({path:path.join(out,'error-'+scheme+'.png'), fullPage:true});
    await page.evaluate(() => {
      emitHistory([{id:'failed',title:'長標題'.repeat(40),status:'failed',error:'https://example.invalid/' + 'long-error-token'.repeat(40)}]);
    });
    await fits(page);
    await page.goto(url + '/setup.html');
    await fits(page);
    await page.screenshot({path:path.join(out,'setup-'+scheme+'.png'), fullPage:true});
    await page.setViewportSize({width:1280,height:900});
    await fits(page);
    await page.screenshot({path:path.join(out,'setup-desktop-'+scheme+'.png'), fullPage:true});
    await context.close();
  }
  for (const width of [320, 375]) {
    const {context,page} = await open('light', width);
    await page.locator('#settings:not([hidden])').waitFor(); await fits(page);
    await page.goto(url + '/setup.html'); await fits(page); await context.close();
  }
  const {context:loadingContext,page:loading} = await open('dark', 384, 'loading');
  await loading.locator('#message.loading').waitFor();
  assert.equal(await loading.locator('#probe').isDisabled(), true);
  assert.equal(await loading.locator('#settings').isVisible(), false);
  await loading.screenshot({path:path.join(out,'loading.png'), fullPage:true});
  await loading.evaluate(() => {window.probeMode = 'ready'; window.finishProbe();});
  await loading.locator('#settings:not([hidden])').waitFor(); await loadingContext.close();
  const {context:unsupportedContext,page:unsupported} = await open('light', 384, 'unsupported');
  await unsupported.getByRole('heading', {name:'開啟 YouTube 影片'}).waitFor();
  assert.equal(await unsupported.locator('#probe').isDisabled(), true);
  assert.equal(await unsupported.evaluate(() => calls.some(m => m.action === 'probe')), false);
  await unsupportedContext.close();
  assert.deepEqual(errors, []);
  console.log('PASS: light/dark contrast ≥4.5, popup ≤600px, 320/375/384px reflow, loading/retry, exact quality, cancel/reveal/clear/help, real progress stages, idle/active controls, long titles/errors, unsupported page, guide.');
} finally {await browser.close(); await new Promise(resolve => server.close(resolve));}
