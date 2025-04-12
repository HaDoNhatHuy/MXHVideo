let pageNumber = 1;
let pageSize = 10;
let searchBy = '';
let categoryId = null; // Đổi mặc định thành null
let utcDateTimeNowString;

function getUtcDateTimeNow() {
    return utcDateTimeNowString;
}

function setUtcDateTimeNow(date) {
    utcDateTimeNowString = date;
}

function getMyVideos() {
    const parameters = {
        pageNumber,
        pageSize,
        searchBy,
        categoryId // categoryId sẽ là null khi không chọn danh mục
    }

    $.ajax({
        url: "/Home/GetVideosForHomeGrid",
        type: "GET",
        data: parameters,
        success: function (data) {
            const result = data.result;

            $('#videosTableBody').empty();
            $('#paginationSummery').empty();
            $('#paginationBtnGroup').empty();
            $('#itemsPerPageDisplay').empty();

            populateVideoTableBody(result.items);

            if (result.totalItemsCount > 0) {
                $('#itemsPerPageDisplay').text(pageSize);

                const from = (result.pageNumber - 1) * result.pageSize + 1;
                const to = result.pageNumber * result.pageSize > result.totalItemsCount ? result.totalItemsCount : result.pageNumber * result.pageSize;
                $('#paginationSummery').text(`${from}-${to} of ${result.totalItemsCount}`);

                // Next Page, Last Page, Previous Page, First Page buttons and functionalities
                let firstPageBtn = '';
                firstPageBtn += `<button type="button" class="btn btn-secondary btn-sm paginationBtn" ${result.pageNumber == 1 ? 'disabled' : ''} data-value="1" data-bs-toggle="tooltip" data-bs-placement="bottom" title="First Page">`;
                firstPageBtn += '<i class="bi bi-chevron-bar-left"></i>';
                firstPageBtn += '</button>';
                $('#paginationBtnGroup').append(firstPageBtn);

                let previousePageBtn = '';
                previousePageBtn += `<button type="button" class="btn btn-secondary btn-sm paginationBtn" ${result.pageNumber == 1 ? 'disabled' : ''} data-value="${result.pageNumber - 1}" data-bs-toggle="tooltip" data-bs-placement="bottom" title="Previous Page">`;
                previousePageBtn += '<i class="bi bi-chevron-left"></i>';
                previousePageBtn += '</button>';
                $('#paginationBtnGroup').append(previousePageBtn);

                let nextPageBtn = '';
                nextPageBtn += `<button type="button" class="btn btn-secondary btn-sm paginationBtn" ${result.pageNumber == result.totalPages ? 'disabled' : ''} data-value="${result.pageNumber + 1}" data-bs-toggle="tooltip" data-bs-placement="bottom" title="Next Page">`;
                nextPageBtn += '<i class="bi bi-chevron-right"></i>';
                nextPageBtn += '</button>';
                $('#paginationBtnGroup').append(nextPageBtn);

                let lastPageBtn = '';
                lastPageBtn += `<button type="button" class="btn btn-secondary btn-sm paginationBtn" ${result.pageNumber == result.totalPages ? 'disabled' : ''}  data-value="${result.totalPages}" data-bs-toggle="tooltip" data-bs-placement="bottom" title="Last Page">`;
                lastPageBtn += '<i class="bi bi-chevron-bar-right"></i>';
                lastPageBtn += '</button>';
                $('#paginationBtnGroup').append(lastPageBtn);

                // On paginationBtn click event
                $('.paginationBtn').click(function () {
                    pageNumber = $(this).data('value');
                    getMyVideos();
                });
            } else {
                $('#itemsPerPageDropdown').hide();
            }
        },
        error: function (xhr, status, error) {
            console.error('Error fetching videos:', error);
            $('#videosTableBody').html('<div class="text-center p-3">Không có video nào để hiển thị.</div>');
            $('#paginationSummery').text('Hiển thị 0 trong số 0 mục');
            $('#paginationBtnGroup').empty();
        }
    });

    // On dropdown "Rows per page" selection event
    $('.pageSizeBtn').click(function () {
        pageSize = $(this).data('value');
        getMyVideos();
    });

    // On dropdown "CategoryDropdown" selection event
    $('#categoryDropdown').on('change', function () {
        var selectedValue = $(this).val();
        categoryId = selectedValue === '0' ? null : selectedValue; // Nếu chọn "Tất cả danh mục", categoryId là null
        getMyVideos();
    });

    // On filter button click
    $('.youtube-filter-btn').on('click', function () {
        $('.youtube-filter-btn').removeClass('active');
        $(this).addClass('active');
        searchBy = $(this).data('filter');
        // Nếu nhấn nút "Tất cả", đặt lại categoryId về null để bỏ lọc theo danh mục
        if (searchBy === 'all') {
            categoryId = null;
            $('#categoryDropdown').val('0'); // Đặt lại dropdown về giá trị mặc định
        }
        getMyVideos();
    });

    function populateVideoTableBody(videos) {
        let html = '';

        if (videos.length > 0) {
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
                                <span class="video-stats">${formatView(v.views)} lượt xem • ${timeAgo(v.createdAt, getUtcDateTimeNow())}</span>
                            </div>
                        </div>
                    </div>`;
            });
        } else {
            html = '<div class="text-center p-3">Không có video nào để hiển thị.</div>';
        }

        $('#videosTableBody').append(html);
    }
}