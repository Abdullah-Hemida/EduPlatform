// admin.js - updated for mobile icons-only default + expand-on-toggle
(function () {
    var NAV_BREAK = 992; // px
    var wrapper = document.getElementById('wrapper');
    var toggle = document.getElementById('sidebarToggle');
    var sidebar = document.getElementById('sidebar-wrapper');
    if (!wrapper || !toggle || !sidebar) return;

    var isRtl = (document.documentElement.getAttribute('dir') || '').toLowerCase() === 'rtl';

    function isMobile() { return window.innerWidth < NAV_BREAK; }

    // make mobile default to icons-only
    function applyMobileCollapsed() {
        wrapper.classList.add('sidebar-collapsed');          // optional: keep collapsed flag consistent
        wrapper.classList.remove('sidebar-expanded-mobile');
        // remove any mobile-hidden classes if present
        wrapper.classList.remove('sidebar-hidden');
        wrapper.classList.remove('sidebar-hidden-rtl');
    }

    // expand to full width on mobile
    function expandMobile() {
        wrapper.classList.add('sidebar-expanded-mobile');
        wrapper.classList.remove('sidebar-collapsed');
    }

    // desktop toggle (icons-only <-> full)
    function toggleDesktopCollapsed() {
        // remove any mobile expanded state
        wrapper.classList.remove('sidebar-expanded-mobile');
        wrapper.classList.remove('sidebar-hidden');
        wrapper.classList.remove('sidebar-hidden-rtl');
        wrapper.classList.toggle('sidebar-collapsed');
    }

    // unified toggle - uses viewport state
    function toggleSidebar() {
        if (isMobile()) {
            if (wrapper.classList.contains('sidebar-expanded-mobile')) {
                applyMobileCollapsed(); // collapse to icons-only
            } else {
                expandMobile(); // expand to full width
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

    // auto apply collapsed on load if mobile
    function initResponsiveState() {
        if (isMobile()) applyMobileCollapsed();
        else {
            // on desktop ensure mobile-expanded not set
            wrapper.classList.remove('sidebar-expanded-mobile');
            // keep current sidebar-collapsed status (do not auto change)
        }
    }

    initResponsiveState();

    window.addEventListener('resize', function () {
        // when crossing breakpoint, restore expected state
        if (isMobile()) {
            // become mobile: default to icons-only (unless previously explicitly expanded)
            if (!wrapper.classList.contains('sidebar-expanded-mobile')) {
                applyMobileCollapsed();
            }
        } else {
            // leave mobile: remove mobile-specific expanded class
            wrapper.classList.remove('sidebar-expanded-mobile');
            // ensure content margin is recalculated by CSS
        }
    });

    // clicking a sidebar link on mobile collapses back to icons-only
    document.addEventListener('click', function (e) {
        var anchor = e.target.closest && e.target.closest('#sidebar-wrapper .nav-link');
        if (anchor && isMobile()) {
            setTimeout(function () {
                applyMobileCollapsed();
            }, 150);
        }
    });

    // initialize bootstrap tooltips (for icon-only mode)
    try {
        var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.forEach(function (el) {
            new bootstrap.Tooltip(el);
        });
    } catch (err) {
        // bootstrap may not be available, swallow quietly
    }

    // expose for debug
    window.adminSidebar = {
        toggle: toggleSidebar,
        expandMobile: expandMobile,
        collapseMobile: applyMobileCollapsed
    };

    document.addEventListener('DOMContentLoaded', function () {
        // Intercept forms with class 'confirm' and optional data-confirm message
        document.querySelectorAll('form.confirm').forEach(function (form) {
            form.addEventListener('submit', function (e) {
                var message = form.getAttribute('data-confirm') || 'Are you sure?';
                if (!window.confirm(message)) {
                    e.preventDefault();
                }
            });
        });

        // Optional: intercept links with class 'confirm-link' and data-confirm (prevents navigation if cancelled)
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
