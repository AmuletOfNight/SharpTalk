window.isMobile = function() {
    return window.innerWidth <= 640;
};

window.checkMobileView = function() {
    return {
        isMobile: window.innerWidth <= 640,
        width: window.innerWidth,
        height: window.innerHeight
    };
};

// Listen for resize events to detect mobile view changes
let resizeTimeout;
window.addEventListener('resize', () => {
    clearTimeout(resizeTimeout);
    resizeTimeout = setTimeout(() => {
        // Notify Blazor component if needed
        const mobileView = window.checkMobileView();
        if (window.mobileViewChanged) {
            window.mobileViewChanged(mobileView);
        }
    }, 250);
});
