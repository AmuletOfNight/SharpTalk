// User Profile Gesture Support
// Provides tap-and-hold functionality for the user profile pill

window.setupUserProfileGestures = function (element, dotNetHelper) {
    if (!element) return;

    window.userProfileDotNetHelper = dotNetHelper;

    let holdTimer = null;
    let isHolding = false;
    let hasMoved = false;
    let startX = 0;
    let startY = 0;
    const holdDuration = 500; // 500ms hold duration

    element.addEventListener('touchstart', function (e) {
        isHolding = true;
        hasMoved = false;
        const touch = e.touches[0];
        startX = touch.clientX;
        startY = touch.clientY;

        holdTimer = setTimeout(function () {
            if (isHolding && !hasMoved) {
                // Trigger hold event
                const event = new CustomEvent('userprofilehold', {
                    bubbles: true,
                    cancelable: true
                });
                element.dispatchEvent(event);
            }
        }, holdDuration);
    }, { passive: true });

    element.addEventListener('touchmove', function (e) {
        if (!isHolding) return;

        const touch = e.touches[0];
        const moveX = Math.abs(touch.clientX - startX);
        const moveY = Math.abs(touch.clientY - startY);

        // If moved more than 10 pixels, cancel the hold
        if (moveX > 10 || moveY > 10) {
            hasMoved = true;
            clearTimeout(holdTimer);
        }
    }, { passive: true });

    element.addEventListener('touchend', function () {
        isHolding = false;
        clearTimeout(holdTimer);
    });

    element.addEventListener('touchcancel', function () {
        isHolding = false;
        clearTimeout(holdTimer);
    });
};

// Close dropdown when clicking outside
document.addEventListener('click', function (e) {
    const dropdown = document.querySelector('.user-dropdown');
    const userProfile = document.querySelector('.user-profile');

    if (dropdown && userProfile) {
        if (!dropdown.contains(e.target) && !userProfile.contains(e.target)) {
            if (window.userProfileDotNetHelper) {
                window.userProfileDotNetHelper.invokeMethodAsync('CloseDropdown');
            }
        }
    }
});
