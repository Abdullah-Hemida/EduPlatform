//// admin.js - updated for mobile icons-only default + expand-on-toggle
// -------------------- Sidebar JS (responsive-aware) --------------------
(function () {
    var NAV_BREAK = 992; // px - matches CSS media query
    var wrapper = document.getElementById('wrapper');
    var toggle = document.getElementById('sidebarToggle');
    var sidebar = document.getElementById('sidebar-wrapper');

    if (!wrapper || !toggle || !sidebar) return;

    var isRtl = (document.documentElement.getAttribute('dir') || '').toLowerCase() === 'rtl';

    function isMobile() {
        return window.innerWidth < NAV_BREAK;
    }

    // default to icon-only on mobile
    function applyMobileCollapsed() {
        wrapper.classList.add('sidebar-collapsed');
        wrapper.classList.remove('sidebar-expanded-mobile');
        // remove hiding flags
        wrapper.classList.remove('sidebar-hidden');
        wrapper.classList.remove('sidebar-hidden-rtl');
        wrapper.classList.remove('sidebar-hidden-full');
        wrapper.classList.remove('sidebar-hidden-full-rtl');
    }

    // expand to full width on mobile
    function expandMobile() {
        wrapper.classList.add('sidebar-expanded-mobile');
        wrapper.classList.remove('sidebar-collapsed');
        // ensure hidden flags are removed
        wrapper.classList.remove('sidebar-hidden');
        wrapper.classList.remove('sidebar-hidden-rtl');
        wrapper.classList.remove('sidebar-hidden-full');
        wrapper.classList.remove('sidebar-hidden-full-rtl');
    }

    // desktop toggle collapse/expand
    function toggleDesktopCollapsed() {
        wrapper.classList.remove('sidebar-expanded-mobile');
        wrapper.classList.remove('sidebar-hidden');
        wrapper.classList.remove('sidebar-hidden-rtl');
        wrapper.classList.toggle('sidebar-collapsed');
    }

    // unified toggle
    function toggleSidebar() {
        if (isMobile()) {
            if (wrapper.classList.contains('sidebar-expanded-mobile')) {
                applyMobileCollapsed();
            } else {
                expandMobile();
            }
        } else {
            toggleDesktopCollapsed();
        }
    }

    // attach toggle
    toggle.addEventListener('click', function (e) {
        e.preventDefault();
        toggleSidebar();
    });

    // init responsive state on load
    function initResponsiveState() {
        if (isMobile()) applyMobileCollapsed();
        else {
            // on desktop ensure mobile-expanded not set
            wrapper.classList.remove('sidebar-expanded-mobile');
            // keep current sidebar-collapsed status as-is
        }
    }

    initResponsiveState();

    window.addEventListener('resize', function () {
        if (isMobile()) {
            // if mobile and not explicitly expanded, show collapsed icon-only
            if (!wrapper.classList.contains('sidebar-expanded-mobile')) {
                applyMobileCollapsed();
            }
        } else {
            // leaving mobile: remove mobile-specific expanded class so desktop CSS takes over
            wrapper.classList.remove('sidebar-expanded-mobile');
        }
    });

    // clicking a sidebar link on mobile collapses back to icons-only
    document.addEventListener('click', function (e) {
        var anchor = e.target.closest && e.target.closest('#sidebar-wrapper .nav-link');
        if (anchor && isMobile()) {
            // small delay so current navigation can run
            setTimeout(function () {
                applyMobileCollapsed();
            }, 150);
        }
    });

    // init bootstrap tooltips (for icon-only mode), safe-guarded
    try {
        var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.forEach(function (el) {
            new bootstrap.Tooltip(el);
        });
    } catch (err) {
        // ignore if bootstrap not available yet
    }

    // expose for debugging
    window.adminSidebar = {
        toggle: toggleSidebar,
        expandMobile: expandMobile,
        collapseMobile: applyMobileCollapsed
    };

    // optional confirmation interceptors (kept from your original script)
    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('form.confirm').forEach(function (form) {
            form.addEventListener('submit', function (e) {
                var message = form.getAttribute('data-confirm') || 'Are you sure?';
                if (!window.confirm(message)) {
                    e.preventDefault();
                }
            });
        });

        document.querySelectorAll('a.confirm-link').forEach(function (a) {
            a.addEventListener('click', function (e) {
                var message = a.getAttribute('data-confirm') || 'Are you sure?';
                if (!window.confirm(message)) {
                    e.preventDefault();
                }
            });
        });
    });
})();

// file: wwwroot/js/layout.js  (or inline in layout after bootstrap bundle)
(function () {
    function moveModalsToBody() {
        document.querySelectorAll('.modal').forEach(function (m) {
            if (m.parentElement !== document.body) {
                document.body.appendChild(m);
            }
        });
    }

    // run on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', moveModalsToBody);
    } else {
        moveModalsToBody();
    }

    // Whenever new modals are dynamically inserted, we try to move them
    var observer = new MutationObserver(function (mutations) {
        mutations.forEach(function (mut) {
            mut.addedNodes.forEach(function (n) {
                if (n.nodeType === 1 && n.matches && n.matches('.modal')) {
                    if (n.parentElement !== document.body) document.body.appendChild(n);
                } else if (n.nodeType === 1) {
                    // also check descendants
                    n.querySelectorAll && n.querySelectorAll('.modal').forEach(function (m) {
                        if (m.parentElement !== document.body) document.body.appendChild(m);
                    });
                }
            });
        });
    });
    observer.observe(document.documentElement || document.body, { childList: true, subtree: true });
})();

// wwwroot/js/layout.js
(function ($) {
    // read localized strings (fallbacks if missing)
    const L = window.localizedStrings || {};
    const msgConfirmDelete = L.confirmDelete || 'Are you sure you want to delete this file?';
    const msgDeleteSuccess = L.deleteSuccess || 'File deleted';
    const msgDeleteFailed = L.deleteFailed || 'Delete failed';
    const msgForbidden = L.forbidden || 'Forbidden';
    const msgInvalidId = L.invalidFileId || 'Invalid file id';
    const msgNoFiles = L.noFiles || 'No files';
    const btnDeleteText = L.deleteConfirmButtonText || 'Delete';


    // quick safety
    if (typeof $ === 'undefined') {
        console.error('layout.js: jQuery not found. Ensure jQuery is loaded before layout.js.');
        return;
    }
    console.debug('layout.js loaded');

    // Read CSRF token
    const csrfToken = $('meta[name="csrf-token"]').attr('content') || '';
    if (!csrfToken) console.warn('layout.js: CSRF token meta tag missing.');

    // Check api urls
    function getApi(key) {
        if (window.fileResourceApi && window.fileResourceApi[key]) return window.fileResourceApi[key];
        const fallback = {
            getDownloadUrl: '/Admin/FileResources/GetDownloadUrl',
            getFiles: '/Admin/FileResources/GetFiles',
            deleteAjax: '/Admin/FileResources/DeleteAjax'
        };
        console.warn('layout.js: window.fileResourceApi.' + key + ' not found; using fallback: ' + fallback[key]);
        return fallback[key];
    }

    // Small toast for visual feedback (uses bootstrap alerts)
    function showToast(msg, type) {
        const containerId = 'file-ajax-toast-container';
        let container = $('#' + containerId);
        if (!container.length) {
            container = $('<div id="' + containerId + '"></div>').css({
                position: 'fixed',
                top: '1rem',
                right: '1rem',
                zIndex: 12000
            });
            $('body').append(container);
        }
        const alert = $('<div class="alert alert-' + (type || 'success') + ' alert-dismissible fade show" role="alert">' +
            msg + '<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button></div>');
        container.append(alert);
        setTimeout(function () { alert.fadeOut(300, function () { $(this).remove(); }); }, 4000);
    }

    // DEBUG: confirm handler attachment
    console.debug('layout.js: attaching handlers for .js-file-download and .js-file-delete');

    // Download handler
    $(document).on('click', 'a.js-file-download', function (e) {
        e.preventDefault();
        e.stopPropagation();

        const $a = $(this);
        const href = $a.attr('href');
        const publicUrl = $a.data('public-url');
        const fileId = $a.data('file-id') || new URL(href, window.location.origin).searchParams.get('id');

        if (publicUrl) {
            // open provider URL immediately
            window.open(publicUrl, '_blank');
            return;
        }

        if (!fileId) {
            window.open(href, '_blank');
            return;
        }

        $.ajax({
            url: getApi('getDownloadUrl'),
            method: 'GET',
            data: { id: fileId },
            dataType: 'json'
        }).done(function (json) {
            console.debug('layout.js: GetDownloadUrl returned', json);
            if (!json || !json.success) {
                window.open(href, '_blank');
                return;
            }
            if (json.publicUrl) {
                // open provider url
                window.open(json.publicUrl, '_blank');
                return;
            }
            if (json.downloadUrl) {
                window.open(json.downloadUrl, '_blank');
                return;
            }
            window.open(href, '_blank');
        }).fail(function (xhr, status, err) {
            console.error('layout.js: GetDownloadUrl failed', status, err);
            window.open(href, '_blank');
        });
    });

    // Delete handler
    $(document).on('click', '.js-file-delete', function (e) {
        e.preventDefault();
        e.stopPropagation();

        const $btn = $(this);
        const fileId = $btn.data('file-id');
        if (!fileId) {
            showToast('Invalid file id', 'danger');
            return;
        }

        if (!confirm(msgConfirmDelete)) return;

        $btn.prop('disabled', true).text('Deleting...');

        // send form-encoded data (default for jQuery) so antiforgery token can be read from body
        $.ajax({
            url: getApi('deleteAjax'),
            method: 'POST',
            data: {
                id: fileId,
                __RequestVerificationToken: csrfToken
            },
            // do not set contentType: let jQuery use application/x-www-form-urlencoded
            dataType: 'json'
        }).done(function (json) {
            console.debug('layout.js: DeleteAjax returned', json);
            if (json && json.success) {
                $('#file-' + fileId).fadeOut(200, function () { $(this).remove(); });
                showToast(msgDeleteSuccess, 'success');
            } else {
                showToast(json && json.message ? json.message : msgDeleteFailed, 'danger');
                if (xhr.status === 403) showToast(msgForbidden, 'danger');
                $btn.prop('disabled', false).text('Delete');
            }
        }).fail(function (xhr) {
            console.error('layout.js: DeleteAjax failed', xhr.status, xhr.responseText);
            if (xhr.status === 403) showToast('Forbidden', 'danger');
            else showToast('Delete failed', 'danger');
            $btn.prop('disabled', false).text('Delete');
        });
    });
    // optional loader...
    window.loadLessonFiles = function (lessonId, $container) {
        $.ajax({
            url: getApi('getFiles'),
            method: 'GET',
            data: { schoolLessonId: lessonId },
            dataType: 'json'
        }).done(function (json) {
            if (!json || !json.success) {
                $container.html('<div class="small text-muted">No files</div>');
                return;
            }
            $container.empty();
            json.files.forEach(function (f) {
                const link = $('<a>')
                    .addClass('js-file-download text-decoration-none')
                    .attr('href', f.downloadUrl)
                    .attr('target', '_blank')
                    .attr('data-file-id', f.id)
                    .text(f.name || ('file-' + f.id));
                const delBtn = $('<button>')
                    .addClass('btn btn-sm btn-outline-danger js-file-delete ms-2')
                    .attr('data-file-id', f.id)
                    .text('Delete');
                const row = $('<div>').attr('id', 'file-' + f.id).addClass('file-entry d-flex align-items-center gap-2 small mb-1');
                row.append(link).append(delBtn);
                $container.append(row);
            });
        }).fail(function () {
            $container.html('<div class="small text-muted">Failed to load files</div>');
        });
    };
})(jQuery);



