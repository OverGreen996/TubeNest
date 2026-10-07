export function videoId(value) {
  try {
    const u = new URL(value);
    if (u.protocol !== 'https:' || u.username || u.password || u.port) return null;
    const id = u.hostname === 'youtu.be' ? u.pathname.slice(1) : ['www.youtube.com','youtube.com','m.youtube.com'].includes(u.hostname) ? u.pathname === '/watch' ? u.searchParams.get('v') : u.pathname.match(/^\/shorts\/([\w-]{11})\/?$/)?.[1] : null;
    return /^[\w-]{11}$/.test(id || '') ? id : null;
  } catch { return null; }
}
export const videoUrl = value => videoId(value) ? `https://www.youtube.com/watch?v=${videoId(value)}` : null;
