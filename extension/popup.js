const $ = id => document.getElementById(id);
let page, info, probing = false, starting = false, working = false;
const finished = status => ['completed', 'cancelled', 'failed'].includes(status);
const labels = {choosing:'選擇儲存位置', downloading:'下載中', merging:'合併影音', verifying:'驗證完整影音', completed:'已儲存 · 驗證通過', cancelled:'已取消', failed:'下載失敗'};
const paths = {
  check: 'm5 12 4 4 10-10',
  clock: 'M12 8v4l3 2M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0',
  alert: 'M12 8v5m0 3h.01M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0',
  folder: 'M3 7V5h6l2 3h10v11H3z',
  cancel: 'm8 8 8 8m0-8-8 8',
  download: 'M12 3v12m-4-4 4 4 4-4M5 17v4h14v-4'
};
function icon(name) {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('aria-hidden', 'true');
  const path = document.createElementNS(svg.namespaceURI, 'path');
  path.setAttribute('d', paths[name]);
  svg.append(path);
  return svg;
}
function message(text, error = false, loading = false) {
  $('message').textContent = text;
  $('message').hidden = !text;
  $('message').classList.toggle('error', error);
  $('message').classList.toggle('loading', loading && !error);
}
async function send(action, data = {}) {
  const reply = await chrome.runtime.sendMessage({action, ...data});
  if (!reply?.ok) throw new Error(reply?.error || '擴充連線失敗。');
  return reply.data;
}
async function run(button, fn) {
  button.disabled = true;
  button.setAttribute('aria-busy', 'true');
  try { await fn(); } catch (e) { message(e.message, true); }
  finally {
    button.disabled = false;
    button.removeAttribute('aria-busy');
    controls();
  }
}
function controls() {
  $('probe').disabled = !page?.url || probing || starting || working;
  $('quality').disabled = probing || starting || working;
  $('download').disabled = !info || probing || starting || working;
  $('probe').setAttribute('aria-busy', String(probing));
  $('probe').title = probing ? '正在讀取畫質…' : '重新讀取畫質';
  $('probe').setAttribute('aria-label', $('probe').title);
  $('settings').setAttribute('aria-busy', String(probing));
  $('download-label').textContent = starting ? '請選擇儲存位置…' : working ? '下載工作進行中' : '下載並選擇儲存位置';
}
function context() { return {url: page.url, tabId: page.tabId}; }
$('help').onclick = () => chrome.runtime.openOptionsPage();
$('probe').onclick = () => readQuality(false);
async function readQuality(reuse) {
  probing = true;
  controls();
  const previous = $('quality').value;
  try {
    message('正在讀取可用畫質，請稍候…', false, true);
    const result = await send('probe', {...context(), reuse});
    info = result;
    $('title').textContent = info.title;
    $('detail').textContent = (info.uploader || 'YouTube') + ' · ' + Math.floor(info.duration / 60) + ' 分 ' + Math.floor(info.duration % 60) + ' 秒';
    $('quality').replaceChildren();
    for (const q of info.qualities) {
      const shorter = Math.min(q.width || q.height, q.height);
      const label = shorter + 'p' + (shorter >= 2160 ? ' · 4K' : '') + (q.width < q.height ? ' · ' + q.width + ' × ' + q.height : '');
      $('quality').add(new Option(label, String(q.height)));
    }
    $('quality').value = info.qualities.some(q => String(q.height) === previous) ? previous : String((info.qualities.find(q => Math.min(q.width || q.height, q.height) <= 1080) || info.qualities[0]).height);
    $('settings').hidden = false;
    message(working ? '下載工作進行中，可在下載紀錄查看進度。' : '選好畫質後，下一步選擇儲存位置。');
  } catch (e) {
    info = undefined;
    $('settings').hidden = true;
    message(e.message, true);
  } finally { probing = false; controls(); }
}
$('download').onclick = () => run($('download'), async () => {
  starting = true;
  controls();
  try {
    message('請在另存新檔視窗選擇位置。', false, true);
    const result = await send('start', {...context(), quality: $('quality').value});
    message(result.cancelled ? '已取消。' : '下載已開始，關閉面板仍會繼續。');
    $('downloads').open = true;
  } finally { starting = false; }
});
function actionButton(text, name, action) {
  const b = document.createElement('button');
  b.className = 'text';
  b.append(icon(name), document.createTextNode(text));
  b.onclick = () => run(b, action);
  return b;
}
function render(history) {
  $('history').replaceChildren();
  $('count').textContent = history.length ? String(history.length) : '';
  $('count').setAttribute('aria-label', history.length + ' 筆紀錄');
  const wasWorking = working;
  const activeJob = history.find(j => !finished(j.status));
  working = !!activeJob;
  if (!starting && !probing && !$('message').classList.contains('error')) {
    if (activeJob) message(activeJob.status === 'choosing' ? '請在另存新檔視窗選擇位置。' : (labels[activeJob.status] || '工作中') + '，可在下載紀錄查看進度。');
    else if (wasWorking && history[0]) {
      const last = history[0];
      message(last.status === 'completed' ? '檔案已儲存，完整影音驗證通過。' : last.status === 'cancelled' ? '已取消。' : last.error || '下載失敗，請查看紀錄。', last.status === 'failed');
    }
  }
  $('state').textContent = working ? '工作中 · 關閉面板仍會繼續' : '按下才啟動 · 完成後退出';
  $('state-dot').classList.toggle('active', working);
  $('clear').hidden = !history.some(j => finished(j.status));
  if (working) $('downloads').open = true;
  if (!history.length) {
    const empty = document.createElement('div');
    empty.className = 'history-empty';
    const title = document.createElement('strong'), hint = document.createElement('p');
    title.textContent = '還沒有下載紀錄';
    hint.textContent = '下載完成後，可以在這裡開啟檔案位置。';
    empty.append(icon('download'), title, hint);
    $('history').append(empty);
  }
  for (const j of history) {
    const item = document.createElement('article'), title = document.createElement('p'), state = document.createElement('p');
    item.className = 'job'; item.dataset.status = j.status;
    title.className = 'job-title'; title.textContent = j.title;
    state.className = 'job-state';
    const percent = j.status === 'downloading' && Number.isFinite(j.progress) && j.progress > 0 ? ' · ' + Math.round(Math.min(100, j.progress)) + '%' : '';
    state.append(icon(j.status === 'completed' ? 'check' : j.status === 'failed' ? 'alert' : 'clock'), document.createTextNode((labels[j.status] || j.status) + percent));
    item.append(title, state);
    if (j.error) {
      const error = document.createElement('p');
      error.className = 'job-error'; error.textContent = j.error; item.append(error);
    }
    if (!finished(j.status)) {
      const progress = document.createElement('progress');
      progress.max = 100;
      progress.setAttribute('aria-label', labels[j.status] || '下載進度');
      if (j.status === 'downloading' && Number.isFinite(j.progress) && j.progress > 0) progress.value = Math.min(100, j.progress);
      item.append(progress);
      if (j.status !== 'choosing') item.append(actionButton('取消下載', 'cancel', () => send('cancel', {id:j.id})));
    }
    if (j.status === 'completed') item.append(actionButton('在資料夾中顯示', 'folder', () => send('reveal', {id:j.id})));
    $('history').append(item);
  }
  controls();
}
$('clear').onclick = () => run($('clear'), () => send('clear'));
chrome.storage.onChanged.addListener((changes, area) => {
  if (area === 'local' && changes.history) render(changes.history.newValue || []);
});
chrome.storage.local.get('history').then(({history = []}) => render(history)).catch(e => message(e.message, true));
send('page').then(result => {
  page = result;
  $('title').textContent = page.url ? page.title : '開啟 YouTube 影片';
  controls();
  if (!page.url) {
    $('detail').textContent = '支援 YouTube 一般影片及 Shorts。';
    message('開啟影片後，再點分享旁的下載按鈕。');
  } else readQuality(true);
}).catch(e => message(e.message, true));
