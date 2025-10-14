// wwwroot/js/home_liked.js
$(document).ready(function () {
    const state = {
        pageNumber: 1,
        pageSize: 12,
        isLoading: false,
        hasMore: true,
        container: $('#likedContainer'),
        apiUrl: '/Home/GetLikesDislikesVideos?liked=true',
        template: 'history'
    };

    // Tạo loading indicator
    const $container = state.container;
    if ($container.length && $container.siblings('#liked-loading-indicator').length === 0) {
        const $loader = $(`<div id="liked-loading-indicator" style="display:none;text-align:center;padding:20px;"><div class="spinner-border spinner-border-sm" role="status"><span class="visually-hidden">Loading...</span></div> Đang tải...</div>`);
        $container.after($loader);
    }

    window.resetAndLoad = function () {
        state.pageNumber = 1;
        state.hasMore = true;
        state.container.empty();
        loadVideos();
    };

    function loadVideos() {
        if (state.isLoading || !state.hasMore) return;
        state.isLoading = true;
        state.container.siblings('#liked-loading-indicator').show();

        const parameters = {
            pageNumber: state.pageNumber,
            pageSize: state.pageSize
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
                            state.container.append('<div class="col-12 text-center p-3">Không có video đã thích nào.</div>');
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
                    console.error('home_liked.js success handler error:', err);
                }
            },
            error: function (xhr, status, err) {
                console.error('Error fetching liked videos:', err);
                state.container.append('<div class="col-12 text-center p-3">Có lỗi khi tải video đã thích.</div>');
            },
            complete: function () {
                state.isLoading = false;
                state.container.siblings('#liked-loading-indicator').hide();
            }
        });
    }

    function populateVideoContainer(videos) {
        let html = '';
        videos.forEach(v => {
            const durationSeconds = parseDurationToSeconds(v.duration || '0:00');
            const progressPercent = durationSeconds > 0 ? Math.min((v.progress / durationSeconds) * 100, 100) : 0;
            const progressTime = formatSecondsToTime(v.progress);

            html += `
                <div class="col-xl-3 col-sm-6 mb-3">
                    <div class="video-card history-video h-100">
                        <div class="video-card-image">
                            <a class="video-close" href="#" data-video-id="${v.id}">
                                <i class="fas fa-times-circle"></i>
                            </a>
                            <a class="play-icon" href="/Video/Watch/${v.id}">
                                <i class="fas fa-play-circle"></i>
                            </a>
                            <a href="/Video/Watch/${v.id}">
                                <img class="img-fluid" src="${v.thumbnail || '/avatarUser/avt-default.jpg'}" alt="${v.title}">
                            </a>
                            <div class="time">${v.duration || '0:00'}</div>
                        </div>
                        ${progressPercent > 0 ? `
                        <div class="progress" style="height: 4px;">
                            <div class="progress-bar bg-danger" role="progressbar" 
                                 style="width: ${progressPercent.toFixed(1)}%;" 
                                 aria-valuenow="${progressPercent}" 
                                 aria-valuemin="0" 
                                 aria-valuemax="100"></div>
                        </div>
                        ` : ''}
                        <div class="video-card-body">
                            <div class="video-title">
                                <a href="/Video/Watch/${v.id}" class="text-truncate">${v.title || 'Untitled Video'}</a>
                            </div>
                            <div class="video-page text-success">
                                ${v.channelName || 'Unknown Channel'} 
                                <a title="Verified" data-bs-placement="top" data-bs-toggle="tooltip" href="#">
                                    <i class="fas fa-check-circle text-success"></i>
                                </a>
                            </div>
                            <div class="video-view text-truncate">
                                ${formatView(v.views || 0)} views
                            </div>
                        </div>
                    </div>
                </div>`;
        });

        if (!videos.length) {
            html = '<div class="col-12 text-center p-3">Không có video đã thích nào.</div>';
        }
        state.container.append(html);

        // Sự kiện xóa
        state.container.find('.video-close').on('click', function (e) {
            e.preventDefault();
            const videoId = $(this).data('video-id');
            $.ajax({
                url: `/Home/RemoveLike?videoId=${videoId}`,
                type: 'POST',
                success: function () {
                    $(this).closest('.col-xl-3').remove();
                    if (state.container.find('.video-card').length === 0) {
                        state.container.append('<div class="col-12 text-center p-3">Không có video đã thích nào.</div>');
                    }
                }.bind(this),
                error: function () {
                    alert('Không thể xóa thích.');
                }
            });
        });
    }

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