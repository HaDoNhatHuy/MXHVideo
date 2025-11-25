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

    // Cập nhật hàm populateVideoContainer trong home_home.js
    // Thay thế phần code xử lý template = 'video' bằng đoạn code này:

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
                // Header cho group mới
                if (page === 'history' && v.groupName !== currentGroup) {
                    currentGroup = v.groupName;
                    html += `<div class="col-12"><h6 class="group-header">${currentGroup || 'Unknown Time'}</h6></div>`;
                }

                const durationSeconds = parseDurationToSeconds(v.duration || '0:00');
                const progressPercent = durationSeconds > 0 ? Math.min((v.progress / durationSeconds) * 100, 100) : 0;

                html += `
        <div class="yt-video-card">
          <a href="/Video/Watch/${v.id}" class="yt-video-thumbnail">
            <img src="${v.thumbnail || '/avatarUser/avt-default.jpg'}" alt="${v.title}">
            <span class="yt-video-duration">${v.duration || '0:00'}</span>
            ${progressPercent > 0 ? `
              <div class="yt-progress">
                <div class="yt-progress-bar" style="width: ${progressPercent.toFixed(1)}%;"></div>
              </div>
            ` : ''}
            <a href="#" class="video-close" data-videoview-id="${v.videoViewId}">
              <i class="fas fa-times"></i>
            </a>
          </a>
          
          <div class="yt-video-info">
            <div class="yt-channel-avatar">
              <a href="/Member/Channel/${v.channelId}">
                <img src="${v.channelAvatar || '/avatarUser/avt-default.jpg'}" alt="${v.channelName}">
              </a>
            </div>
            
            <div class="yt-video-details">
              <div class="yt-video-title">
                <a href="/Video/Watch/${v.id}">${v.title || 'Untitled Video'}</a>
              </div>
              
              <div class="yt-channel-name">
                <a href="/Member/Channel/${v.channelId}">${v.channelName || 'Unknown Channel'}</a>
                <i class="fas fa-check-circle"></i>
              </div>
              
              <div class="yt-video-meta">
                ${formatView(v.views || 0)} • ${v.lastVisitTimeAgo || 'Unknown'}
              </div>
            </div>
          </div>
        </div>
      `;
            });

            if (!uniqueVideos.length) {
                html = `<div class="col-12 text-center p-3">Không có lịch sử xem nào.</div>`;
            }

        } else {
            // ===== TEMPLATE STANDARD VIDEO (INDEX PAGE) =====
            videos.forEach(v => {
                const durationStr = v.duration ? formatDuration(v.duration) : '0:00';
                const avatarUrl = v.channelAvatar || '/avatarUser/avt-default.jpg';
                const thumbnailUrl = v.thumbnail || '/avatarUser/avt-default.jpg';

                html += `
        <div class="yt-video-card">
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
                const videoViewId = $(this).data('videoview-id');

                $.ajax({
                    url: `/Home/RemoveHistory?videoViewId=${videoViewId}`,
                    type: 'POST',
                    success: function () {
                        $(this).closest('.yt-video-card').remove();

                        if ($container.find('.yt-video-card').length === 0) {
                            $container.append('<div class="col-12 text-center p-3">Không có lịch sử xem nào.</div>');
                        }
                    }.bind(this),
                    error: function () {
                        alert('Không thể xóa lịch sử.');
                    }
                });
            });
        } else if (page === 'liked') {
            $container.find('.video-close').on('click', function (e) {
                e.preventDefault();
                const videoId = $(this).data('video-id');

                $.ajax({
                    url: `/Home/RemoveLike?videoId=${videoId}`,
                    type: 'POST',
                    success: function () {
                        $(this).closest('.yt-video-card').remove();

                        if ($container.find('.yt-video-card').length === 0) {
                            $container.append('<div class="col-12 text-center p-3">Không có video đã thích nào.</div>');
                        }
                    }.bind(this),
                    error: function () {
                        alert('Không thể xóa thích.');
                    }
                });
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