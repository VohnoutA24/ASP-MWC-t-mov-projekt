// School Portal - Site JavaScript

// Navigation scroll effect
document.addEventListener('DOMContentLoaded', function () {
    const nav = document.getElementById('mainNav');
    const navToggle = document.getElementById('navToggle');
    const navMenu = document.getElementById('navMenu');

    // Scroll effect for nav
    if (nav) {
        window.addEventListener('scroll', function () {
            if (window.scrollY > 20) {
                nav.classList.add('scrolled');
            } else {
                nav.classList.remove('scrolled');
            }
        });
    }

    // Mobile menu toggle
    if (navToggle && navMenu) {
        navToggle.addEventListener('click', function () {
            navMenu.classList.toggle('active');
            navToggle.classList.toggle('active');
        });

        // Close menu on link click
        navMenu.querySelectorAll('a').forEach(function (link) {
            link.addEventListener('click', function () {
                navMenu.classList.remove('active');
                navToggle.classList.remove('active');
            });
        });
    }

    // Intersection Observer for animations
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
                observer.unobserve(entry.target);
            }
        });
    }, observerOptions);

    document.querySelectorAll('.feature-card, .stat-card, .dash-card, .schedule-card, .info-highlight-card').forEach(function (el) {
        el.style.opacity = '0';
        el.style.transform = 'translateY(20px)';
        el.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
        observer.observe(el);
    });

    // Form input focus effects
    document.querySelectorAll('.form-input').forEach(function (input) {
        input.addEventListener('focus', function () {
            this.closest('.form-group')?.classList.add('focused');
        });
        input.addEventListener('blur', function () {
            this.closest('.form-group')?.classList.remove('focused');
        });
    });

    // Theme toggle
    const themeToggle = document.getElementById('themeToggle');
    if (themeToggle) {
        const themeIcon = themeToggle.querySelector('i');
        const themeText = themeToggle.querySelector('span');

        const updateToggleUI = (theme) => {
            if (theme === 'dark') {
                themeIcon.classList.replace('fa-moon', 'fa-sun');
                themeText.textContent = 'Světlý režim';
            } else {
                themeIcon.classList.replace('fa-sun', 'fa-moon');
                themeText.textContent = 'Tmavý režim';
            }
        };

        // Sync UI on load
        updateToggleUI(document.documentElement.getAttribute('data-theme'));

        themeToggle.addEventListener('click', function () {
            const currentTheme = document.documentElement.getAttribute('data-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
            
            document.body.classList.add('theme-transitioning');
            document.documentElement.setAttribute('data-theme', newTheme);
            localStorage.setItem('theme', newTheme);
            updateToggleUI(newTheme);

            setTimeout(() => {
                document.body.classList.remove('theme-transitioning');
            }, 400);
        });
    }
});
