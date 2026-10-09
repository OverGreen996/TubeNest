import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
const root = path.resolve('test-output/browser-compat');
const read = (browser, name) => JSON.parse(fs.readFileSync(path.join(root,browser,'ui-states',name),'utf8'));
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const report = {edge:read('edge','browser.json'),chrome:read('chrome','browser.json'),layouts:{},screenshots:{}};
for (const theme of ['light','dark']) {
  const edge = read('edge','layout-'+theme+'.json'), chrome = read('chrome','layout-'+theme+'.json');
  assert.deepEqual(Object.keys(edge),Object.keys(chrome));
  const differences = [];
  for (const [selector,values] of Object.entries(edge)) for (const [property,value] of Object.entries(values)) {
    const other = chrome[selector][property];
    const delta = typeof value === 'number' ? Math.abs(value-other) : value === other ? 0 : null;
    if (delta !== 0) differences.push({selector,property,edge:value,chrome:other,delta});
    if (typeof value === 'number') assert.ok(delta <= 1,theme+' '+selector+' '+property+' difference >1px');
    else assert.equal(other,value,theme+' '+selector+' '+property);
  }
  report.layouts[theme] = {measuredElements:Object.keys(edge).length,differences};
  const name = 'popup-'+theme+'.png';
  const hashes = ['edge','chrome'].map(browser=>hash(path.join(root,browser,'ui-states',name)));
  report.screenshots[theme] = {identicalPng:hashes[0]===hashes[1],edgeSHA256:hashes[0],chromeSHA256:hashes[1]};
}
fs.writeFileSync(path.join(root,'comparison.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));
console.log('PASS: identical UI text styles/colors/radii and layout within 1px in Chrome for Testing and Edge.');
