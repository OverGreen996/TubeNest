import path from 'node:path';

export const testBrowser = process.env.TUBENEST_TEST_BROWSER || 'edge';
if (!['edge', 'chrome'].includes(testBrowser)) throw new Error('TUBENEST_TEST_BROWSER must be edge or chrome');
export const launchOptions = testBrowser === 'chrome'
  ? {headless:true, executablePath:path.resolve(process.env.TUBENEST_TEST_CHROME || 'test-output/chrome-compat/chrome-win64/chrome.exe')}
  : {headless:true, channel:'msedge'};
export const outputDirectory = (name, original) => path.resolve(process.env.TUBENEST_TEST_BROWSER ? 'test-output/browser-compat/' + testBrowser + '/' + name : original);
