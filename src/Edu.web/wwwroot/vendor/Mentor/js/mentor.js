/**
* Template Name: Mentor
* Template URL: https://bootstrapmade.com/mentor-free-education-bootstrap-theme/
* Updated: Aug 07 2024 with Bootstrap v5.3.3
* Author: BootstrapMade.com
* License: https://bootstrapmade.com/license/
*/

(function () {
    "use strict";

    /**
     * Apply .scrolled class to the body as the page is scrolled down
     */
    function toggleScrolled() {
        const selectBody = document.querySelector('body');
        // try header by id first, then class
        const selectHeader = document.querySelector('#header') || document.querySelector('.navbar');
        if (!selectHeader) return; // nothing to do if no header
        // Only proceed if header has one of the sticky classes
        if (!selectHeader.classList.contains('scroll-up-sticky') &&
            !selectHeader.classList.contains('sticky-top') &&
            !selectHeader.classList.contains('fixed-top')) return;

        if (window.scrollY > 100) {
            selectBody && selectBody.classList.add('scrolled');
        } else {
            selectBody && selectBody.classList.remove('scrolled');
        }
    }

    // safe attach
    document.addEventListener('scroll', toggleScrolled);
    window.addEventListener('load', toggleScrolled);

    /**
     * Mobile nav toggle
     */
    // try to find either the specific mobile toggle or the bootstrap navbar-toggler
    const mobileNavToggleBtn = document.querySelector('.mobile-nav-toggle') || document.querySelector('.navbar-toggler');

    function mobileNavToogle() {
        const body = document.querySelector('body');
        if (body) body.classList.toggle('mobile-nav-active');

        if (mobileNavToggleBtn) {
            mobileNavToggleBtn.classList.toggle('bi-list');
            mobileNavToggleBtn.classList.toggle('bi-x');
        }
    }

    if (mobileNavToggleBtn) {
        mobileNavToggleBtn.addEventListener('click', mobileNavToogle);
    }

    /**
     * Hide mobile nav on same-page/hash links
     */
    const navmenuLinks = document.querySelectorAll('#navmenu a');
    if (navmenuLinks && navmenuLinks.length) {
        navmenuLinks.forEach(navmenu => {
            navmenu.addEventListener('click', () => {
                if (document.querySelector('.mobile-nav-active')) {
                    mobileNavToogle();
                }
            });
        });
    }

    /**
     * Toggle mobile nav dropdowns
     */
    const toggleDropdowns = document.querySelectorAll('.navmenu .toggle-dropdown');
    if (toggleDropdowns && toggleDropdowns.length) {
        toggleDropdowns.forEach(navmenu => {
            navmenu.addEventListener('click', function (e) {
                e.preventDefault();
                if (this.parentNode) {
                    this.parentNode.classList.toggle('active');
                    if (this.parentNode.nextElementSibling)
                        this.parentNode.nextElementSibling.classList.toggle('dropdown-active');
                }
                e.stopImmediatePropagation();
            });
        });
    }

    /**
     * Preloader
     */
    const preloader = document.querySelector('#preloader');
    if (preloader) {
        window.addEventListener('load', () => {
            preloader.remove();
        });
    }

    /**
     * Scroll top button
     */
    let scrollTop = document.querySelector('.scroll-top');

    function toggleScrollTop() {
        if (scrollTop) {
            window.scrollY > 100 ? scrollTop.classList.add('active') : scrollTop.classList.remove('active');
        }
    }

    if (scrollTop) {
        scrollTop.addEventListener('click', (e) => {
            e.preventDefault();
            window.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        });
    }

    window.addEventListener('load', toggleScrollTop);
    document.addEventListener('scroll', toggleScrollTop);

    /**
     * Animation on scroll function and init
     */
    function aosInit() {
        if (typeof AOS !== 'undefined' && AOS.init) {
            AOS.init({
                duration: 600,
                easing: 'ease-in-out',
                once: true,
                mirror: false
            });
        }
    }
    window.addEventListener('load', aosInit);

    /**
     * Initiate glightbox (safe)
     */
    if (typeof GLightbox !== 'undefined') {
        try {
            const glightbox = GLightbox({
                selector: '.glightbox'
            });
        } catch (e) {
            // ignore
        }
    }

    /**
     * Initiate Pure Counter (safe)
     */
    if (typeof PureCounter !== 'undefined') {
        try {
            new PureCounter();
        } catch (e) { }
    }

    /**
     * Init swiper sliders
     */
    function initSwiper() {
        document.querySelectorAll(".init-swiper").forEach(function (swiperElement) {
            try {
                let configEl = swiperElement.querySelector(".swiper-config");
                if (!configEl) return;
                let config = JSON.parse(configEl.innerHTML.trim());

                if (swiperElement.classList.contains("swiper-tab")) {
                    initSwiperWithCustomPagination(swiperElement, config);
                } else {
                    new Swiper(swiperElement, config);
                }
            } catch (e) {
                // ignore malformed config
                console.warn('Swiper init error:', e);
            }
        });
    }

    window.addEventListener("load", initSwiper);

})();
