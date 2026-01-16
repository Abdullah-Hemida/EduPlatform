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

function showAlert(message, type = 'success') {
    const alertContainer = document.getElementById('alert-container');
    const alert = document.createElement('div');
    alert.className = `alert alert-${type} alert-dismissible fade show shadow alert-floating mt-2`;
    alert.role = 'alert';
    alert.innerHTML = `
    ${message}
    <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `;
    alertContainer.appendChild(alert);
    setTimeout(() => alert.remove(), 4000);
}
