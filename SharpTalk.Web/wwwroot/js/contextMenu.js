// Context Menu Support

window.setupContextMenu = function (dotNetHelper) {
    window.contextMenuDotNetHelper = dotNetHelper;
};

document.addEventListener('click', function (e) {
    const menu = document.querySelector('.context-menu');
    if (menu && !menu.contains(e.target)) {
        if (window.contextMenuDotNetHelper) {
            window.contextMenuDotNetHelper.invokeMethodAsync('HideContextMenu');
        }
    }
});

// Close on escape key
document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        const menu = document.querySelector('.context-menu');
        if (menu && window.contextMenuDotNetHelper) {
            window.contextMenuDotNetHelper.invokeMethodAsync('HideContextMenu');
        }
    }
});
