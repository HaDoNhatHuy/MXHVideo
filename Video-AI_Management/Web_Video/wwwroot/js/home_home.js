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

        let excludedIds = [];
        $('.yt-video-card').each(function () {
            excludedIds.push($(this).data('video-id'));
        });

        const parameters = {
            pageNumber: st.pageNumber,
            pageSize: st.pageSize,
            excludeIds: excludedIds // Gửi mảng này về server
        };

        if (page === 'index') {
            parameters.searchBy = st.searchBy;
            if (st.categoryId && st.categoryId !== '0') {
                parameters.categoryId = st.categoryId;
            }
        }

        $.ajax({
            url: st.apiUrl,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(parameters),
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

    function populateVideoContainer($container, videos, template, page) {
        let html = '';

        if (template === 'history') {
            let currentGroup = null;
            const groupedVideos = {};

            // Lọc trùng lặp
            videos.forEach(v => {
                if (!groupedVideos[v.id] || new Date(v.lastVisit) > new Date(groupedVideos[v.id].lastVisit)) {
                    groupedVideos[v.id] = v;
                }
            });

            const uniqueVideos = Object.values(groupedVideos);

            uniqueVideos.forEach(v => {
                // Header cho group mới (chỉ cho History page)
                if (page === 'history' && v.groupName !== currentGroup) {
                    currentGroup = v.groupName;
                    html += `<div class="group-header">${currentGroup || 'Unknown Time'}</div>`;
                }

                const durationSeconds = parseDurationToSeconds(v.duration || '0:00');
                const progressPercent = durationSeconds > 0 ? Math.min((v.progress / durationSeconds) * 100, 100) : 0;
                const progressTime = formatSecondsToTime(v.progress || 0);

                html += `
            <div class="yt-list-video-card">
                <a href="/Video/Watch/${v.id}" class="yt-list-thumbnail">
                    <img src="${v.thumbnail || '/avatarUser/avt-default.jpg'}" alt="${v.title}">
                    <span class="yt-list-duration">${v.duration || '0:00'}</span>
                    ${page === 'history' && progressPercent > 0 ? `
                    <div class="yt-progress">
                        <div class="yt-progress-bar" style="width: ${progressPercent.toFixed(1)}%;"></div>
                    </div>
                    ` : ''}
                    <button class="video-close" data-${page === 'history' ? 'videoview' : 'video'}-id="${page === 'history' ? v.videoViewId : v.id}" title="Xóa khỏi ${page === 'history' ? 'lịch sử' : 'danh sách đã thích'}">
                        <i class="fas fa-times"></i>
                    </button>
                </a>
                <div class="yt-list-metadata">
                    <h3 class="yt-list-title">
                        <a href="/Video/Watch/${v.id}">${v.title || 'Untitled Video'}</a>
                    </h3>
                    <div class="yt-list-channel">
                        <div class="yt-list-channel-avatar">
                            <a href="/Member/Channel/${v.channelId}">
                                <img src="${v.channelAvatar || '/avatarUser/avt-default.jpg'}" alt="${v.channelName}">
                            </a>
                        </div>
                        <div class="yt-list-channel-name">
                            <a href="/Member/Channel/${v.channelId}">${v.channelName || 'Unknown Channel'}</a>
                            <i class="fas fa-check-circle"></i>
                        </div>
                    </div>
                    <div class="yt-list-meta">
                        ${formatView(v.views || 0)} • ${page === 'history' ? (v.lastVisitTimeAgo || 'Unknown') : (v.createdAtTimeAgo || 'Vừa xong')}
                    </div>
                    ${v.description ? `<div class="yt-list-description">${v.description}</div>` : ''}
                </div>
            </div>
            `;
            });

            if (!uniqueVideos.length) {
                html = `
            <div class="empty-state">
                <i class="fas fa-${page === 'history' ? 'history' : 'heart'}"></i>
                <h3>${page === 'history' ? 'Không có lịch sử xem' : 'Chưa có video nào được thích'}</h3>
                <p>${page === 'history' ? 'Các video bạn xem sẽ hiển thị ở đây' : 'Các video bạn thích sẽ hiển thị ở đây'}</p>
            </div>
            `;
            }

        } else {
            // ===== TEMPLATE STANDARD VIDEO (INDEX PAGE) - GIỮ NGUYÊN =====
            videos.forEach(v => {
                const durationStr = v.duration ? formatDuration(v.duration) : '0:00';
                const avatarUrl = v.channelAvatar || '/avatarUser/avt-default.jpg';
                const thumbnailUrl = v.thumbnail || '/avatarUser/avt-default.jpg';

                html += `
                  <div class="yt-video-card" data-video-id="${v.id}">
                    <a href="/Video/Watch/${v.id}" class="yt-video-thumbnail">
                        <img src="${thumbnailUrl}" alt="${v.title}">
                        <span class="yt-video-duration">${durationStr}</span>
                    </a>

                    <div class="yt-video-info">
                        <div class="yt-channel-avatar">
                            <a href="/Member/Channel/${v.channelId}">
                                <img src="${avatarUrl}" alt="${v.channelName}">
                            </a>
                        </div>

                        <div class="yt-video-details">
                            <div class="yt-video-title">
                                <a href="/Video/Watch/${v.id}" title="${v.title}">
                                    ${v.title || 'Untitled Video'}
                                </a>
                            </div>

                            <div class="yt-channel-name">
                                <a href="/Member/Channel/${v.channelId}">
                                    ${v.channelName || 'Unknown Channel'}
                                </a>
                                <i class="fas fa-check-circle"></i>
                            </div>

                            <div class="yt-video-meta">
                                ${formatView(v.views || 0)} • ${v.createdAtTimeAgo || 'Vừa xong'}
                            </div>
                        </div>
                         <!-- NÚT 3 CHẤM -->
                            <div class="video-actions dropdown mt-1">
                                <button class="btn btn-link btn-sm text-secondary" data-toggle="dropdown">
                                    <i class="fas fa-ellipsis-v"></i>
                                </button>

                                <ul class="dropdown-menu dropdown-menu-end">
                                    <li>
                                        <a class="dropdown-item not-interested-btn" href="#" data-id="${v.id}">
                                            <i class="fas fa-ban me-2"></i> Not interested
                                        </a>
                                    </li>
                                    <li>
                                        <a class="dropdown-item dont-recommend-btn" href="#" data-channel-id="${v.channelId}">
                                            <i class="fas fa-user-slash me-2"></i> Don't recommend channel
                                        </a>
                                    </li>
                                </ul>
                            </div>
                    </div>
                </div>

            `;
            });

            if (!videos.length) {
                html = `<div class="col-12 text-center p-3">Không có ${page === 'liked' ? 'video đã thích' : 'video'} nào.</div>`;
            }
        }

        $container.append(html);

        // Xử lý sự kiện xóa
        if (template === 'history') {
            $container.find('.video-close').on('click', function (e) {
                e.preventDefault();
                const $card = $(this).closest('.yt-list-video-card');

                if (page === 'history') {
                    const videoViewId = $(this).data('videoview-id');
                    $.ajax({
                        url: `/Home/RemoveHistory?videoViewId=${videoViewId}`,
                        type: 'POST',
                        success: function () {
                            $card.remove();
                            if ($container.find('.yt-list-video-card').length === 0) {
                                $container.html(`
                                <div class="empty-state">
                                    <i class="fas fa-history"></i>
                                    <h3>Không có lịch sử xem</h3>
                                    <p>Các video bạn xem sẽ hiển thị ở đây</p>
                                </div>
                            `);
                            }
                        },
                        error: function () {
                            alert('Không thể xóa lịch sử.');
                        }
                    });
                } else if (page === 'liked') {
                    const videoId = $(this).data('video-id');
                    $.ajax({
                        url: `/Home/RemoveLike?videoId=${videoId}`,
                        type: 'POST',
                        success: function () {
                            $card.remove();
                            if ($container.find('.yt-list-video-card').length === 0) {
                                $container.html(`
                                <div class="empty-state">
                                    <i class="fas fa-heart"></i>
                                    <h3>Chưa có video nào được thích</h3>
                                    <p>Các video bạn thích sẽ hiển thị ở đây</p>
                                </div>
                            `);
                            }
                        },
                        error: function () {
                            alert('Không thể xóa thích.');
                        }
                    });
                }
            });
        }
    }

    // Hàm format duration từ TimeSpan hoặc string
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

    // NOT INTERESTED
    $(document).on('click', '.not-interested-btn', function (e) {
        e.preventDefault();
        const videoId = $(this).data('id');
        const $card = $(this).closest('.yt-video-card');

        $.post('/Home/BlockContent', { targetId: videoId, type: 'Video' }, function () {
            $card.fadeOut();
        });
    });

    // DON'T RECOMMEND CHANNEL
    $(document).on('click', '.dont-recommend-btn', function (e) {
        e.preventDefault();
        const channelId = $(this).data('channel-id');
        const $card = $(this).closest('.yt-video-card');

        $.post('/Home/BlockContent', { targetId: channelId, type: 'Channel' }, function () {

            // Ẩn tất cả video của channel này
            $(`.yt-video-card[data-channel-id="${channelId}"]`).fadeOut();

            // Ẩn card hiện tại
            $card.fadeOut();
        });
    });

    function isElementScrollable($el) {
        if (!$el || !$el.length) return false;
        const el = $el[0];
        return (el.scrollHeight > el.clientHeight) && (getComputedStyle(el).overflowY === 'auto' || getComputedStyle(el).overflowY === 'scroll');
    }

    // Gọi load lần đầu cho trang hiện tại
    $(document).ready(function () {
        const currentPage = $('body').data('page');
        if (currentPage && state[currentPage.toLowerCase()]) {
            window.resetAndLoad(currentPage.toLowerCase());
        }
    });

    attachScrollHandlers();
})();