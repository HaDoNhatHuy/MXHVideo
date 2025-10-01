let searchBy = '';
window.getMyVideos = function () {
    const query = document.getElementById('searchInput').value.trim();
    if (query) {
        searchBy = query;
        window.location.href = `/Search?query=${encodeURIComponent(query)}`;
    }
};