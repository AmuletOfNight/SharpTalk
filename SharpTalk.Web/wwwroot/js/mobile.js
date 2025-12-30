let dotNetHelper;

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

window.registerMobileViewCallback = function(dotNetObj) {
    dotNetHelper = dotNetObj;
};

// Listen for resize events to detect mobile view changes
let resizeTimeout;
window.addEventListener('resize', () => {
    clearTimeout(resizeTimeout);
    resizeTimeout = setTimeout(() => {
        const mobile = window.isMobile();
        
        // Notify Blazor component if helper is registered
        if (dotNetHelper) {
            dotNetHelper.invokeMethodAsync('OnMobileViewChanged', mobile);
        }
        
        // Legacy support if needed
        if (window.mobileViewChanged) {
            window.mobileViewChanged(window.checkMobileView());
        }
    }, 250);
});
