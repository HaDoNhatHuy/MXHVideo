// wwwroot/js/home_home.js
// Phiên bản: Infinite Scroll (đóng scope, an toàn, robust)
(function () {
    // nếu jQuery chưa load thì báo và dừng
    if (typeof jQuery === 'undefined') {
        console.error('home_home.js: jQuery chưa được load. Hãy đảm bảo jQuery được include TRƯỚC file này trong _Layout.cshtml.');
        return;
    }

    const $ = jQuery;

    // trạng thái (private trong scope)
    let pageNumber = 1;
    let pageSize = 10;
    let searchBy = '';
    let categoryId = null;
    let utcDateTimeNowString = null;

    let isLoading = false;
    let hasMore = true;

    const $videosBody = $('#videosTableBody'); // container của grid
    // nếu không tồn tại, cố gắng lấy container khác (#dataContainer)
    const $container = $videosBody.length ? $videosBody : $('#dataContainer');

    // tạo loading indicator nếu chưa có
    if ($('#home-loading-indicator').length === 0) {
        const $loader = $('<div id="home-loading-indicator" style="display:none;text-align:center;padding:10px;"><div class="spinner-border spinner-border-sm" role="status"><span class="visually-hidden">Loading...</span></div> Đang tải...</div>');
        $container.after($loader);
    }

    // expose minimal API ra global vì partial/Index.cshtml có gọi getMyVideos() trực tiếp
    window.setUtcDateTimeNow = function (date) { utcDateTimeNowString = date; };
    window.getUtcDateTimeNow = function () { return utcDateTimeNowString; };
    window.resetAndLoad = function () {
        pageNumber = 1;
        hasMore = true;
        $container.empty();
        getMyVideos();
    };

    // Gọi API để lấy dữ liệu
    window.getMyVideos = function () {
        if (isLoading || !hasMore) return;
        isLoading = true;
        $('#home-loading-indicator').show();

        const parameters = {
            pageNumber: pageNumber,
            pageSize: pageSize,
            searchBy: searchBy,
            // server uses Guid.Empty for "no category": HomeParameters.CategoryId type is Guid
            // we send null or empty string to indicate "all"; server-side HomeParameters expects Guid - 
            // previously server checks parameters.CategoryId != Guid.Empty; if you pass null it might break model binding.
            // To be safe, only send categoryId if not null; otherwise omit it.
        };

        // add categoryId only if set (client uses null when no category)
        if (categoryId && categoryId !== '0') {
            parameters.categoryId = categoryId;
        }

        $.ajax({
            url: "/Home/GetVideosForHomeGrid",
            type: "GET",
            data: parameters,
            success: function (data) {
                try {
                    const result = data.result;
                    if (!result || !result.items) {
                        // no data -> finish
                        hasMore = false;
                        return;
                    }

                    // append videos
                    populateVideoTableBody(result.items);

                    // nếu server trả ít hơn pageSize => hết dữ liệu
                    if (result.items.length < pageSize || (result.totalItemsCount && (pageNumber * pageSize) >= result.totalItemsCount)) {
                        hasMore = false;
                    } else {
                        pageNumber++;
                    }
                } catch (err) {
                    console.error('home_home.js success handler error:', err);
                }
            },
            error: function (xhr, status, err) {
                console.error('Error fetching videos:', err);
            },
            complete: function () {
                isLoading = false;
                $('#home-loading-indicator').hide();
            }
        });
    };

    // render html từng video (bạn có thể điều chỉnh template tuỳ ý)
    function populateVideoTableBody(videos) {
        let html = '';

        if (!videos || videos.length === 0) {
            // nếu lần đầu và ko có video, hiển thị message
            if (pageNumber === 1) {
                html = '<div class="text-center p-3">Không có video nào để hiển thị.</div>';
                $container.append(html);
            }
            return;
        }

        videos.forEach(v => {
            html += `
                <div class="youtube-video-card">
                    <a href="/Video/Watch/${v.id}" class="thumbnail-link">
                        <img src="${v.thumbnail}" alt="Video Thumbnail" class="thumbnail-img" />
                    </a>
                    <div class="video-details">
                        <a href="/Video/Watch/${v.id}" class="video-title">${v.title}</a>
                        <div class="video-meta">
                            <a href="/Member/Channel/${v.channelId}" class="channel-name">${v.channelName}</a>
                            <span class="video-stats">${formatView(v.views || 0)} lượt xem • ${timeAgo(v.createdAt, getUtcDateTimeNow())}</span>
                        </div>
                    </div>
                </div>`;
        });

        $container.append(html);
    }

    // Các event UI (rows per page, category filter, filter buttons)
    $(document).on('click', '.pageSizeBtn', function () {
        pageSize = parseInt($(this).data('value')) || 10;
        window.resetAndLoad();
    });

    $(document).on('change', '#categoryDropdown', function () {
        const selectedValue = $(this).val();
        // nếu dropdown trả '0' cho All
        categoryId = (selectedValue === '0' || !selectedValue) ? null : selectedValue;
        window.resetAndLoad();
    });

    $(document).on('click', '.youtube-filter-btn', function () {
        $('.youtube-filter-btn').removeClass('active');
        $(this).addClass('active');
        searchBy = $(this).data('filter') || '';
        if (searchBy === 'all') {
            categoryId = null;
            $('#categoryDropdown').val('0');
        }
        window.resetAndLoad();
    });

    // ---------- scroll handler: bắt scroll trên window hoặc trên container scrollable nếu container có overflow ----------
    function isElementScrollable($el) {
        if (!$el || !$el.length) return false;
        const el = $el[0];
        return (el.scrollHeight > el.clientHeight) && (getComputedStyle(el).overflowY === 'auto' || getComputedStyle(el).overflowY === 'scroll');
    }

    function attachScrollHandlers() {
        // nếu container có scroll riêng (ví dụ #videosTableBody có overflow:auto), lắng nghe trên chính container
        if (isElementScrollable($container)) {
            $container.on('scroll', function () {
                const el = this;
                if (el.scrollTop + el.clientHeight >= el.scrollHeight - 200) {
                    getMyVideos();
                }
            });
        } else {
            // listen on window
            $(window).on('scroll', function () {
                if ($(window).scrollTop() + $(window).height() >= $(document).height() - 200) {
                    getMyVideos();
                }
            });
        }
    }

    // gọi attach một lần
    attachScrollHandlers();

    // NOTE: không tự gọi getMyVideos() ở đây để tránh gọi 2 lần nếu partial/index đã gọi sẵn.
    // Partial (Index.cshtml) của bạn có đoạn gọi getMyVideos() khi cần (do đó ta expose getMyVideos global).
})();
