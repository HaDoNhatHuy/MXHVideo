// wwwroot/js/home_utils.js
if (typeof jQuery === 'undefined') {
    console.error('home_utils.js: jQuery chưa được load. Hãy đảm bảo jQuery được include TRƯỚC file này trong _Layout.cshtml.');
    return;
}

const $ = jQuery;

function parseDurationToSeconds(duration) {
    if (typeof duration === 'string') {
        const [min, sec] = duration.split(':').map(Number);
        return (min * 60) + sec;
    }
    return Math.floor(duration.TotalSeconds || 0);
}

function formatSecondsToTime(seconds) {
    const min = Math.floor(seconds / 60);
    const sec = Math.floor(seconds % 60);
    return `${min}:${sec < 10 ? '0' : ''}${sec}`;
}

function formatDuration(duration) {
    const totalSeconds = typeof duration === 'string' ? parseDurationToSeconds(duration) : Math.floor(duration.TotalSeconds || 0);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${minutes}:${seconds < 10 ? '0' : ''}${seconds}`;
}

function formatView(views) {
    if (views >= 1000000) return `${(views / 1000000).toFixed(1)}M views`;
    if (views >= 1000) return `${(views / 1000).toFixed(1)}K views`;
    return `${views} views`;
}

function isElementScrollable($el) {
    if (!$el || !$el.length) return false;
    const el = $el[0];
    return (el.scrollHeight > el.clientHeight) && (getComputedStyle(el).overflowY === 'auto' || getComputedStyle(el).overflowY === 'scroll');
}