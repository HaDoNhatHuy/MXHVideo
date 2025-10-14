// wwwroot/js/home_index.js
$(document).ready(function () {
    const state = {
        pageNumber: 1,
        pageSize: 12,
        searchBy: 'all',
        categoryId: null,
        isLoading: false,
        hasMore: true,
        container: $('#videosTableBody'),
        apiUrl: '/Home/GetVideosForHomeGrid',
        template: 'video'
    };

    let utcDateTimeNowString = null;

    // Tạo loading indicator
    const $container = state.container;
    if ($container.length && $container.siblings('#index-loading-indicator').length === 0) {
        const $loader = $(`<div id="index-loading-indicator" style="display:none;text-align:center;padding:20px;"><div class="spinner-border spinner-border-sm" role="status"><span class="visually-hidden">Loading...</span></div> Đang tải...</div>`);
        $container.after($loader);
    }

    // Expose API toàn cục
    window.setUtcDateTimeNow = function (date) { utcDateTimeNowString = date; };
    window.getUtcDateTimeNow = function () { return utcDateTimeNowString; };
    window.resetAndLoad = function () {
        state.pageNumber = 1;
        state.hasMore = true;
        state.container.empty();
        loadVideos();
    };

    function loadVideos() {
        if (state.isLoading || !state.hasMore) return;
        state.isLoading = true;
        state.container.siblings('#index-loading-indicator').show();

        const parameters = {
            pageNumber: state.pageNumber,
            pageSize: state.pageSize,
            searchBy: state.searchBy,
            categoryId: state.categoryId
        };

        $.ajax({
            url: state.apiUrl,
            type: 'GET',
            data: parameters,
            success: function (data) {
                try {
                    const result = data.result;
                    if (!result || !result.items) {
                        state.hasMore = false;
                        if (state.pageNumber === 1) {
                            state.container.append('<div class="col-12 text-center p-3">Không có video nào.</div>');
                        }
                        return;
                    }

                    populateVideoContainer(result.items);

                    if (result.items.length < state.pageSize || (result.totalItemsCount && (state.pageNumber * state.pageSize) >= result.totalItemsCount)) {
                        state.hasMore = false;
                    } else {
                        state.pageNumber++;
                    }
                } catch (err) {
                    console.error('home_index.js success handler error:', err);
                }
            },
            error: function (xhr, status, err) {
                console.error('Error fetching index videos:', err);
                state.container.append('<div class="col-12 text-center p-3">Có lỗi khi tải video.</div>');
            },
            complete: function () {
                state.isLoading = false;
                state.container.siblings('#index-loading-indicator').hide();
            }
        });
    }

    function populateVideoContainer(videos) {
        let html = '';
        videos.forEach(v => {
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
                                ${formatView(v.views || 0)} &nbsp;<i class="fas fa-calendar-alt"></i> ${v.createdAtTimeAgo || 'Unknown Time'}
                            </div>
                        </div>
                    </div>
                </div>`;
        });
        if (!videos.length) {
            html = '<div class="col-12 text-center p-3">Không có video nào.</div>';
        }
        state.container.append(html);
    }

    // Sự kiện UI
    $(document).on('click', '.category-tab', function () {
        const $this = $(this);
        $('.category-tab').removeClass('active');
        $this.addClass('active');
        state.categoryId = $this.data('category-id') === '0' ? null : $this.data('category-id');
        window.resetAndLoad();
    });

    $(document).on('click', '.youtube-filter-btn', function () {
        $('.youtube-filter-btn').removeClass('active');
        $(this).addClass('active');
        state.searchBy = $(this).data('filter') || 'all';
        if (state.searchBy === 'all') {
            state.categoryId = null;
            $('.category-tab').removeClass('active');
            $('.category-tab[data-category-id="0"]').addClass('active');
        }
        window.resetAndLoad();
    });

    // Infinite scroll
    if (state.container.length) {
        if (isElementScrollable(state.container)) {
            state.container.on('scroll', function () {
                const el = this;
                if (el.scrollTop + el.clientHeight >= el.scrollHeight - 200) {
                    loadVideos();
                }
            });
        } else {
            $(window).on('scroll', function () {
                if ($(window).scrollTop() + $(window).height() >= $(document).height() - 200) {
                    loadVideos();
                }
            });
        }
    }

    window.resetAndLoad();
});