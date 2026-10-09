import {chromium} from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {launchOptions,outputDirectory,testBrowser} from './test-browser.mjs';
const out = outputDirectory('live-page','test-output/live-page');
fs.mkdirSync(out,{recursive:true});
const extension = path.resolve('extension');
const context = await chromium.launchPersistentContext(path.join(out,'profile-'+Date.now()),{
  ...launchOptions,viewport:{width:1280,height:900},colorScheme:'dark',
  args:['--disable-extensions-except='+extension,'--load-extension='+extension]
});
const report = {browser:testBrowser,version:context.browser().version(),pages:[]};
try {
  const page = await context.newPage();
  for (const [kind,url] of [['watch','https://www.youtube.com/watch?v=yRRQ6filg-E'],['shorts','https://www.youtube.com/shorts/18NGQq7p3LY']]) {
    await page.goto(url,{waitUntil:'domcontentloaded',timeout:45000});
    const button = page.locator('#tubenest-download button');
    await button.waitFor({state:'visible',timeout:30000});
    const result = await page.locator('#tubenest-download').evaluate(host=>{
      const button = host.shadowRoot.querySelector('button'), style = getComputedStyle(button), rect = button.getBoundingClientRect();
      const previous = host.previousElementSibling;
      return {
        url:location.href,dark:host.hasAttribute('dark'),shorts:host.hasAttribute('shorts'),
        text:button.textContent.trim(),ariaLabel:button.getAttribute('aria-label'),
        marginLeft:getComputedStyle(host).marginLeft,marginRight:getComputedStyle(host).marginRight,
        previousLabel:previous?.textContent.trim(),
        previousButtons:[...previous?.querySelectorAll('button')||[]].map(b=>b.getAttribute('aria-label')),
        color:style.color,svgStroke:getComputedStyle(button.querySelector('svg')).stroke,
        background:style.backgroundColor,borderRadius:style.borderRadius,
        buttonWidth:rect.width,buttonHeight:rect.height,x:rect.x,y:rect.y,
        viewportWidth:innerWidth,viewportHeight:innerHeight
      };
    });
    assert.equal(result.ariaLabel,'使用 TubeNest 下載影片');
    assert.equal(result.color,result.svgStroke);
    assert.equal(result.shorts,kind==='shorts');
    assert.ok(result.previousButtons.some(label=>/分享|share/i.test(label||'')) || /分享|share/i.test(result.previousLabel||''),'Button follows Share on the live YouTube page');
    assert.ok(result.x>=0 && result.x+result.buttonWidth<=result.viewportWidth);
    if(kind==='watch') {assert.equal(result.marginLeft,'8px');assert.equal(result.marginRight,'8px');}
    report.pages.push({kind,...result});
    await page.screenshot({path:path.join(out,kind+'.png')});
  }
  fs.writeFileSync(path.join(out,'result.json'),JSON.stringify(report,null,2));
  console.log('PASS ('+testBrowser+'): real public YouTube watch/Shorts pages load the extension button after Share, in theme, within viewport.');
} finally {await context.close();}
