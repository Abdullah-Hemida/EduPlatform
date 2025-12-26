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


//(function () {
//    var NAV_BREAK = 992; // px
//    var wrapper = document.getElementById('wrapper');
//    var toggle = document.getElementById('sidebarToggle');
//    var sidebar = document.getElementById('sidebar-wrapper');
//    if (!wrapper || !toggle || !sidebar) return;

//    var isRtl = (document.documentElement.getAttribute('dir') || '').toLowerCase() === 'rtl';

//    function isMobile() { return window.innerWidth < NAV_BREAK; }

//    // make mobile default to icons-only
//    function applyMobileCollapsed() {
//        wrapper.classList.add('sidebar-collapsed');          // optional: keep collapsed flag consistent
//        wrapper.classList.remove('sidebar-expanded-mobile');
//        // remove any mobile-hidden classes if present
//        wrapper.classList.remove('sidebar-hidden');
//        wrapper.classList.remove('sidebar-hidden-rtl');
//    }

//    // expand to full width on mobile
//    function expandMobile() {
//        wrapper.classList.add('sidebar-expanded-mobile');
//        wrapper.classList.remove('sidebar-collapsed');
//    }

//    // desktop toggle (icons-only <-> full)
//    function toggleDesktopCollapsed() {
//        // remove any mobile expanded state
//        wrapper.classList.remove('sidebar-expanded-mobile');
//        wrapper.classList.remove('sidebar-hidden');
//        wrapper.classList.remove('sidebar-hidden-rtl');
//        wrapper.classList.toggle('sidebar-collapsed');
//    }

//    // unified toggle - uses viewport state
//    function toggleSidebar() {
//        if (isMobile()) {
//            if (wrapper.classList.contains('sidebar-expanded-mobile')) {
//                applyMobileCollapsed(); // collapse to icons-only
//            } else {
//                expandMobile(); // expand to full width
//            }
//        } else {
//            toggleDesktopCollapsed();
//        }
//    }

//    // attach toggle
//    toggle.addEventListener('click', function (e) {
//        e.preventDefault();
//        toggleSidebar();
//    });

//    // auto apply collapsed on load if mobile
//    function initResponsiveState() {
//        if (isMobile()) applyMobileCollapsed();
//        else {
//            // on desktop ensure mobile-expanded not set
//            wrapper.classList.remove('sidebar-expanded-mobile');
//            // keep current sidebar-collapsed status (do not auto change)
//        }
//    }

//    initResponsiveState();

//    window.addEventListener('resize', function () {
//        // when crossing breakpoint, restore expected state
//        if (isMobile()) {
//            // become mobile: default to icons-only (unless previously explicitly expanded)
//            if (!wrapper.classList.contains('sidebar-expanded-mobile')) {
//                applyMobileCollapsed();
//            }
//        } else {
//            // leave mobile: remove mobile-specific expanded class
//            wrapper.classList.remove('sidebar-expanded-mobile');
//            // ensure content margin is recalculated by CSS
//        }
//    });

//    // clicking a sidebar link on mobile collapses back to icons-only
//    document.addEventListener('click', function (e) {
//        var anchor = e.target.closest && e.target.closest('#sidebar-wrapper .nav-link');
//        if (anchor && isMobile()) {
//            setTimeout(function () {
//                applyMobileCollapsed();
//            }, 150);
//        }
//    });

//    // initialize bootstrap tooltips (for icon-only mode)
//    try {
//        var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
//        tooltipTriggerList.forEach(function (el) {
//            new bootstrap.Tooltip(el);
//        });
//    } catch (err) {
//        // bootstrap may not be available, swallow quietly
//    }

//    // expose for debug
//    window.adminSidebar = {
//        toggle: toggleSidebar,
//        expandMobile: expandMobile,
//        collapseMobile: applyMobileCollapsed
//    };

//    document.addEventListener('DOMContentLoaded', function () {
//        // Intercept forms with class 'confirm' and optional data-confirm message
//        document.querySelectorAll('form.confirm').forEach(function (form) {
//            form.addEventListener('submit', function (e) {
//                var message = form.getAttribute('data-confirm') || 'Are you sure?';
//                if (!window.confirm(message)) {
//                    e.preventDefault();
//                }
//            });
//        });

//        // Optional: intercept links with class 'confirm-link' and data-confirm (prevents navigation if cancelled)
//        document.querySelectorAll('a.confirm-link').forEach(function (a) {
//            a.addEventListener('click', function (e) {
//                var message = a.getAttribute('data-confirm') || 'Are you sure?';
//                if (!window.confirm(message)) {
//                    e.preventDefault();
//                }
//            });
//        });
//    });
//})();
//(function () {
//    // Uses only existing #sidebarToggle in topbar
//    var BREAK = 992;
//    var wrapper = document.getElementById('wrapper');
//    var sidebar = document.getElementById('sidebar-wrapper');
//    var toggle = document.getElementById('sidebarToggle');
//    if (!wrapper || !sidebar) return;

//    var storageKey = 'adminSidebarCollapsed';
//    var isRtl = (document.documentElement.getAttribute('dir') || '').toLowerCase() === 'rtl';

//    function isMobile() { return window.innerWidth < BREAK; }

//    function applyMargins() {
//        // set margin on #page-content-wrapper based on current wrapper state and direction
//        var page = document.getElementById('page-content-wrapper');
//        if (!page) return;
//        var computedWidth = getComputedStyle(document.documentElement).getPropertyValue('--sidebar-width').trim() || '250px';
//        var collapsed = getComputedStyle(document.documentElement).getPropertyValue('--sidebar-collapsed').trim() || '80px';
//        var collapsedMobile = getComputedStyle(document.documentElement).getPropertyValue('--sidebar-collapsed-mobile').trim() || '56px';

//        var widthToUse;
//        if (isMobile()) {
//            if (wrapper.classList.contains('sidebar-expanded-mobile')) widthToUse = computedWidth;
//            else widthToUse = collapsedMobile;
//        } else {
//            if (wrapper.classList.contains('sidebar-collapsed')) widthToUse = collapsed;
//            else widthToUse = computedWidth;
//        }

//        // clear previous inline margins first
//        page.style.marginLeft = '';
//        page.style.marginRight = '';

//        if (isRtl) {
//            page.style.marginRight = widthToUse;
//            page.style.marginLeft = '0';
//        } else {
//            page.style.marginLeft = widthToUse;
//            page.style.marginRight = '0';
//        }
//    }

//    function setCollapsedDesktop(collapsed) {
//        if (collapsed) wrapper.classList.add('sidebar-collapsed'); else wrapper.classList.remove('sidebar-collapsed');
//        // remove mobile expanded state
//        wrapper.classList.remove('sidebar-expanded-mobile');
//        try { localStorage.setItem(storageKey, collapsed ? '1' : '0'); } catch (e) { }
//        applyMargins();
//        refreshTooltips();
//    }

//    function expandMobile() {
//        wrapper.classList.add('sidebar-expanded-mobile');
//        wrapper.classList.remove('sidebar-collapsed');
//        applyMargins();
//        refreshTooltips();
//    }

//    function collapseMobile() {
//        wrapper.classList.remove('sidebar-expanded-mobile');
//        wrapper.classList.add('sidebar-collapsed');
//        applyMargins();
//        refreshTooltips();
//    }

//    // tooltips management (enable only when text hidden)
//    var ttInstances = [];
//    function refreshTooltips() {
//        try { ttInstances.forEach(function (t) { if (t && t.dispose) t.dispose(); }); } catch (e) { }
//        ttInstances = [];
//        // nav-text visible?
//        var textVisible = document.querySelector('.nav-text') && window.getComputedStyle(document.querySelector('.nav-text')).display !== 'none';
//        if (textVisible) return;
//        try {
//            var els = [].slice.call(document.querySelectorAll('#sidebar-wrapper [data-bs-toggle="tooltip"]'));
//            els.forEach(function (el) {
//                var inst = bootstrap.Tooltip.getOrCreateInstance(el, { placement: isRtl ? 'left' : 'right', boundary: 'viewport' });
//                ttInstances.push(inst);
//            });
//        } catch (e) { /* bootstrap not present */ }
//    }

//    // init state
//    function init() {
//        var saved = null;
//        try { saved = localStorage.getItem(storageKey); } catch (e) { }
//        if (isMobile()) {
//            // mobile default: collapsed (thin)
//            collapseMobile();
//        } else {
//            if (saved === '1') setCollapsedDesktop(true); else setCollapsedDesktop(false);
//        }
//        applyMargins();
//        setTimeout(refreshTooltips, 250);
//    }

//    // wire topbar toggle if present
//    if (toggle) {
//        toggle.addEventListener('click', function (e) {
//            e.preventDefault();
//            if (isMobile()) {
//                if (wrapper.classList.contains('sidebar-expanded-mobile')) collapseMobile();
//                else expandMobile();
//            } else {
//                setCollapsedDesktop(!wrapper.classList.contains('sidebar-collapsed'));
//            }
//        });
//    }

//    // clicking a nav link on mobile collapses back (good UX)
//    document.addEventListener('click', function (ev) {
//        var link = ev.target.closest && ev.target.closest('#sidebar-wrapper .nav-link');
//        if (link && isMobile()) {
//            setTimeout(collapseMobile, 120);
//        }
//    });

//    // keyboard activation
//    document.querySelectorAll('#sidebar-wrapper .nav-link').forEach(function (el) {
//        el.setAttribute('tabindex', '0');
//        el.addEventListener('keydown', function (e) {
//            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); el.click(); }
//        });
//    });

//    // on resize adapt
//    var resizeTimer = null;
//    window.addEventListener('resize', function () {
//        clearTimeout(resizeTimer);
//        resizeTimer = setTimeout(function () {
//            if (isMobile()) collapseMobile();
//            else {
//                var saved = null;
//                try { saved = localStorage.getItem(storageKey); } catch (e) { }
//                if (saved === '1') setCollapsedDesktop(true); else setCollapsedDesktop(false);
//            }
//            applyMargins();
//            refreshTooltips();
//        }, 120);
//    });

//    init();
//})();


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
