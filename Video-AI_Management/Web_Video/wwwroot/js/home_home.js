(function () {
    if (typeof jQuery === 'undefined') {
        console.error('home_home.js: jQuery chưa được load. Hãy đảm bảo jQuery được include TRƯỚC file này trong _Layout.cshtml.');
        return;
    }

    const $ = jQuery;

    // Trạng thái cho từng trang
    const state = {
        index: {
            pageNumber: 1,
            pageSize: 12,
            searchBy: 'all',
            categoryId: null,
            isLoading: false,
            hasMore: true,
            container: $('#videosTableBody'),
            apiUrl: '/Home/GetVideosForHomeGrid',
            template: 'video'
        },
        history: {
            pageNumber: 1,
            pageSize: 12,
            isLoading: false,
            hasMore: true,
            container: $('#historyContainer'),
            apiUrl: '/Home/GetHistory',
            template: 'history'
        },
        liked: {
            pageNumber: 1,
            pageSize: 12,
            isLoading: false,
            hasMore: true,
            container: $('#likedContainer'),
            apiUrl: '/Home/GetLikesDislikesVideos?liked=true',
            template: 'history'
        }
    };

    let utcDateTimeNowString = null;

    // Tạo loading indicator cho từng container nếu chưa có
    Object.keys(state).forEach(key => {
        const $container = state[key].container;
        if ($container.length && $container.siblings(`#${key}-loading-indicator`).length === 0) {
            const $loader = $(`<div id="${key}-loading-indicator" style="display:none;text-align:center;padding:20px;"><div class="spinner-border spinner-border-sm" role="status"><span class="visually-hidden">Loading...</span></div> Đang tải...</div>`);
            $container.after($loader);
        }
    });

    // Expose API toàn cục
    window.setUtcDateTimeNow = function (date) { utcDateTimeNowString = date; };
    window.getUtcDateTimeNow = function () { return utcDateTimeNowString; };
    window.resetAndLoad = function (page) {
        const st = state[page];
        if (!st) return;
        st.pageNumber = 1;
        st.hasMore = true;
        st.container.empty();
        loadVideos(page);
    };

    // Hàm tải video chung
    function loadVideos(page) {
        const st = state[page];
        if (!st || st.isLoading || !st.hasMore) return;
        st.isLoading = true;
        st.container.siblings(`#${page}-loading-indicator`).show();

        const parameters = {
            pageNumber: st.pageNumber,
            pageSize: st.pageSize
        };

        if (page === 'index') {
            parameters.searchBy = st.searchBy;
            if (st.categoryId && st.categoryId !== '0') {
                parameters.categoryId = st.categoryId;
            }
        }

        $.ajax({
            url: st.apiUrl,
            type: 'GET',
            data: parameters,
            success: function (data) {
                try {
                    const result = data.result;
                    if (!result || !result.items) {
                        st.hasMore = false;
                        if (st.pageNumber === 1) {
                            st.container.append(`<div class="col-12 text-center p-3">Không có ${page === 'history' ? 'lịch sử xem' : page === 'liked' ? 'video đã thích' : 'video'} nào.</div>`);
                        }
                        return;
                    }

                    populateVideoContainer(st.container, result.items, st.template, page);

                    if (result.items.length < st.pageSize || (result.totalItemsCount && (st.pageNumber * st.pageSize) >= result.totalItemsCount)) {
                        st.hasMore = false;
                    } else {
                        st.pageNumber++;
                    }
                } catch (err) {
                    console.error(`home_home.js success handler error (${page}):`, err);
                }
            },
            error: function (xhr, status, err) {
                console.error(`Error fetching ${page} videos:`, err);
                st.container.append(`<div class="col-12 text-center p-3">Có lỗi khi tải ${page === 'history' ? 'lịch sử xem' : page === 'liked' ? 'video đã thích' : 'video'}.</div>`);
            },
            complete: function () {
                st.isLoading = false;
                st.container.siblings(`#${page}-loading-indicator`).hide();
            }
        });
    }

    // Render HTML cho video
    function populateVideoContainer($container, videos, template, page) {
        let html = '';
        videos.forEach(v => {
            if (template === 'history') {
                const durationStr = v.duration ? formatDuration(v.duration) : '3:50';
                const progressTime = v.progress ? Math.floor(v.progress / 100 * (v.duration ? v.duration.TotalSeconds : 230)) : '1:40';
                html += `
                    <div class="col-xl-3 col-sm-6 mb-3">
                        <div class="video-card history-video h-100">
                            <div class="video-card-image">
                                <a class="video-close" href="#" data-video-id="${v.id}"><i class="fas fa-times-circle"></i></a>
                                <a class="play-icon" href="/Video/Watch/${v.id}"><i class="fas fa-play-circle"></i></a>
                                <a href="/Video/Watch/${v.id}">
                                    <img class="img-fluid" src="${v.thumbnail || '/avatarUser/avt-default.jpg'}" alt="Video Thumbnail">
                                </a>
                                <div class="time">${durationStr}</div>
                            </div>
                            <div class="progress">
                                <div class="progress-bar" role="progressbar" style="width: ${v.progress || 0}%;" aria-valuenow="${v.progress || 0}" aria-valuemin="0" aria-valuemax="100">${progressTime}</div>
                            </div>
                            <div class="video-card-body">
                                <div class="video-title">
                                    <a href="/Video/Watch/${v.id}" class="text-truncate">${v.title || 'Untitled Video'}</a>
                                </div>
                                <div class="video-page text-success">
                                    ${v.channelName || 'Unknown Channel'} <a title="" data-bs-placement="top" data-bs-toggle="tooltip" href="#" data-bs-original-title="Verified"><i class="fas fa-check-circle text-success"></i></a>
                                </div>
                                <div class="video-view text-truncate">
                                    ${formatView(v.views || 0)} &nbsp;<i class="fas fa-calendar-alt"></i> ${page === 'history' ? (v.lastVisitTimeAgo || 'Unknown Time') : (v.createdAtTimeAgo || 'Unknown Time')}
                                </div>
                            </div>
                        </div>
                    </div>`;
            } else {
                const durationStr = v.duration ? formatDuration(v.duration) : '3:50';
                html += `
                    <div class="col-xl-3 col-sm-6 mb-3">
                        <div class="video-card h-100">
                            <div class="video-card-image">
                                <a class="play-icon" href="/Video/Watch/${v.id}"><i class="fas fa-play-circle"></i></a>
                                <a href="/Video/Watch/${v.id}">
                                    <img class="img-fluid" src="${v.thumbnail || '/avatarUser/avt-default.jpg'}" alt="Video Thumbnail">
                                </a>
                                <div class="time">${durationStr}</div>
                            </div>
                            <div class="video-card-body">
                                <div class="video-title">
                                    <a href="/Video/Watch/${v.id}" class="text-truncate">${v.title || 'Untitled Video'}</a>
                                </div>
                                <div class="video-page text-success">
                                    ${v.channelName || 'Unknown Channel'} <a title="" data-bs-placement="top" data-bs-toggle="tooltip" href="#" data-bs-original-title="Verified"><i class="fas fa-check-circle text-success"></i></a>
                                </div>
                                <div class="video-view text-truncate">
                                    ${formatView(v.views || 0)} &nbsp;<i class="fas fa-calendar-alt"></i> ${v.createdAtTimeAgo || 'Unknown Time'} <!-- Sử dụng createdAtTimeAgo -->
                                </div>
                            </div>
                        </div>
                    </div>`;
            }
        });
        $container.append(html);

        // Thêm sự kiện xóa cho history và liked
        if (template === 'history') {
            $container.find('.video-close').on('click', function (e) {
                e.preventDefault();
                const videoId = $(this).data('video-id');
                const url = page === 'history' ? `/Home/RemoveHistory?videoId=${videoId}` : `/Home/RemoveLike?videoId=${videoId}`;
                $.ajax({
                    url: url,
                    type: 'POST',
                    success: function () {
                        $(this).closest('.col-xl-3').remove();
                    }.bind(this),
                    error: function () {
                        alert(`Không thể xóa ${page === 'history' ? 'lịch sử' : 'thích'}.`);
                    }
                });
            });
        }
    }

    // Hàm format duration từ TimeSpan
    function formatDuration(duration) {
        const totalSeconds = Math.floor(duration.TotalSeconds);
        const minutes = Math.floor(totalSeconds / 60);
        const seconds = totalSeconds % 60;
        return `${minutes}:${seconds < 10 ? '0' : ''}${seconds}`;
    }

    // Sự kiện UI cho Index
    $(document).on('click', '.category-tab', function () {
        const $this = $(this);
        $('.category-tab').removeClass('active');
        $this.addClass('active');
        state.index.categoryId = $this.data('category-id') === '0' ? null : $this.data('category-id');
        window.resetAndLoad('index');
    });

    $(document).on('click', '.youtube-filter-btn', function () {
        $('.youtube-filter-btn').removeClass('active');
        $(this).addClass('active');
        state.index.searchBy = $(this).data('filter') || 'all';
        if (state.index.searchBy === 'all') {
            state.index.categoryId = null;
            $('.category-tab').removeClass('active');
            $('.category-tab[data-category-id="0"]').addClass('active');
        }
        window.resetAndLoad('index');
    });

    // Xử lý infinite scroll
    function attachScrollHandlers() {
        Object.keys(state).forEach(page => {
            const $container = state[page].container;
            if ($container.length) {
                if (isElementScrollable($container)) {
                    $container.on('scroll', function () {
                        const el = this;
                        if (el.scrollTop + el.clientHeight >= el.scrollHeight - 200) {
                            loadVideos(page);
                        }
                    });
                } else {
                    $(window).on('scroll', function () {
                        if ($(window).scrollTop() + $(window).height() >= $(document).height() - 200) {
                            loadVideos(page);
                        }
                    });
                }
            }
        });
    }

    function isElementScrollable($el) {
        if (!$el || !$el.length) return false;
        const el = $el[0];
        return (el.scrollHeight > el.clientHeight) && (getComputedStyle(el).overflowY === 'auto' || getComputedStyle(el).overflowY === 'scroll');
    }

    // Gọi load lần đầu cho trang hiện tại
    $(document).ready(function () {
        const currentPage = $('body').data('page'); // Được set trong ViewData["CurrentPage"]
        if (currentPage && state[currentPage.toLowerCase()]) {
            window.resetAndLoad(currentPage.toLowerCase());
        }
    });

    attachScrollHandlers();
})();