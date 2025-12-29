# Mobile UI Fixes Plan

## Overview
This plan addresses multiple mobile UI issues in the SharpTalk application, focusing on improving the user experience on mobile devices.

## Issues Identified

### 1. User Pill Covers Chat Message Input
**Problem:** The user pill at the bottom completely covers the chat message box.

**Root Cause:** 
- `.user-profile-container` is positioned at `bottom: 16px; left: 16px;` with `z-index: 1000`
- On mobile, it spans full width (`right: 16px`) which overlaps with `.message-input`
- The message input area doesn't account for the user pill's presence

**Solution:**
- Adjust the user pill positioning to be in the lower left corner only (not full width)
- Add bottom padding to the message input area to prevent overlap
- Ensure the user pill doesn't interfere with typing

### 2. User Pill Shows Too Much Information
**Problem:** The user pill currently shows avatar, username, status text, and dropdown arrow, taking up too much space.

**Root Cause:**
- The `.user-profile` component displays all user info by default
- No tap-and-hold gesture to expand the pill

**Solution:**
- Redesign the user pill to show only the profile picture (avatar) by default
- Implement tap-and-hold gesture to expand the pill and show menu options
- The expanded state should show the username and menu items

### 3. User Menu Animation is Ugly
**Problem:** The user popup from the user pill is ugly and clashes with the design.

**Root Cause:**
- `.user-dropdown` has basic styling with no smooth animation
- The menu appears abruptly without a transition from the pill shape

**Solution:**
- Create a smooth upward animation that reveals menu items from the shape of the pill
- Use CSS transforms and transitions for a polished effect
- The menu should expand outward from the pill's position

### 4. Duplicate Hamburger Menu
**Problem:** Sometimes there is a duplicate hamburger menu at the top.

**Root Cause:**
- `MobileNavigation` component shows a hamburger menu button
- There may be another hamburger menu appearing in certain views

**Solution:**
- Review all components to identify where duplicate menus appear
- Ensure only one hamburger menu is visible at any time
- Remove or conditionally hide duplicate menu buttons

### 5. Workspace Name Header Issues
**Problem:** The workspace name looks ugly and conflicts with the back arrow.

**Root Cause:**
- The `.nav-title` in `MobileNavigation` shows the view title
- The `.workspace-name-header` in `ChannelList` also shows workspace name
- Both may be visible simultaneously on mobile, causing conflicts

**Solution:**
- Hide the workspace name header from `ChannelList` when on mobile
- Use only the `MobileNavigation` title for consistency
- Improve the styling of the nav title to be more visually appealing

### 6. Settings Button Overlaps with Hamburger Menu
**Problem:** The settings button for the workspace and hamburger menu are showing up on top of each other.

**Root Cause:**
- The workspace settings button in `ChannelList` (`.settings-trigger`)
- The hamburger menu button in `MobileNavigation` (`.menu-btn`)
- Both are positioned in the header area and may overlap

**Solution:**
- Hide the workspace settings button when on mobile
- Move workspace settings access to a different location (e.g., inside the channel list or a separate menu)
- Ensure proper spacing between header elements

## Implementation Plan

### Phase 1: User Pill Redesign

#### 1.1 Update UserProfile.razor
- Simplify the user pill to show only the avatar by default
- Add tap-and-hold gesture detection
- Implement expanded state with menu items
- Add smooth animation for menu reveal

#### 1.2 Update app.css
- Modify `.user-profile-container` positioning for mobile
- Add new styles for compact user pill (avatar-only)
- Add styles for expanded user pill with menu
- Create smooth animation keyframes for menu reveal
- Add bottom padding to message input area

### Phase 2: Mobile Navigation Fixes

#### 2.1 Update MobileNavigation.razor
- Review and remove any duplicate menu buttons
- Ensure only one hamburger menu is visible
- Improve nav title styling

#### 2.2 Update ChannelList.razor
- Hide workspace name header on mobile
- Hide settings button on mobile
- Move workspace settings access to a more appropriate location

#### 2.3 Update MainLayout.razor.css
- Ensure proper z-index layering for mobile elements
- Fix any overlapping issues

### Phase 3: Testing and Refinement

#### 3.1 Test on Mobile Devices
- Verify user pill doesn't cover message input
- Test tap-and-hold gesture functionality
- Verify smooth menu animation
- Check for duplicate menus
- Verify workspace name display
- Check settings button placement

#### 3.2 Refine Styling
- Adjust spacing and positioning as needed
- Fine-tune animations for smoothness
- Ensure consistent styling across all mobile views

## Technical Details

### User Pill States

**Compact State (Default):**
- Shows only the profile picture (avatar)
- Positioned in lower left corner
- Small circular button (40px diameter)
- Tap-and-hold to expand

**Expanded State:**
- Shows avatar, username, and menu items
- Smooth upward animation from pill shape
- Menu items: Settings, Logout
- Tap outside or tap again to collapse

### CSS Changes Required

```css
/* Mobile User Pill - Compact State */
@media (max-width: 640.98px) {
    .user-profile-container {
        bottom: 20px;
        left: 20px;
        right: auto;
        max-width: 48px;
    }

    .user-profile {
        padding: 8px;
        border-radius: 50%;
        width: 48px;
        height: 48px;
        justify-content: center;
    }

    .user-info,
    .dropdown-arrow {
        display: none;
    }

    .user-avatar {
        width: 32px;
        height: 32px;
    }

    /* Expanded State */
    .user-profile.expanded {
        width: auto;
        max-width: 200px;
        border-radius: 24px;
        padding: 8px 16px;
    }

    .user-profile.expanded .user-info,
    .user-profile.expanded .dropdown-arrow {
        display: flex;
    }

    /* Smooth Menu Animation */
    .user-dropdown {
        transform-origin: bottom left;
        transform: scale(0.8) translateY(10px);
        opacity: 0;
        transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    }

    .user-dropdown.visible {
        transform: scale(1) translateY(0);
        opacity: 1;
    }
}

/* Message Input - Add Bottom Padding */
@media (max-width: 640.98px) {
    .message-input {
        padding-bottom: 80px;
    }
}

/* Hide Workspace Header on Mobile */
@media (max-width: 640.98px) {
    .workspace-name-header {
        display: none;
    }
}
```

### JavaScript Changes Required

Add tap-and-hold gesture detection:

```javascript
// Add to mobile.js or create new file
function setupTapAndHold(element, callback, holdDuration = 500) {
    let holdTimer;
    let isHolding = false;

    element.addEventListener('touchstart', (e) => {
        isHolding = true;
        holdTimer = setTimeout(() => {
            if (isHolding) {
                callback(e);
            }
        }, holdDuration);
    });

    element.addEventListener('touchend', () => {
        isHolding = false;
        clearTimeout(holdTimer);
    });

    element.addEventListener('touchmove', () => {
        isHolding = false;
        clearTimeout(holdTimer);
    });
}
```

## Files to Modify

1. `SharpTalk.Web/Shared/UserProfile.razor` - Redesign user pill
2. `SharpTalk.Web/wwwroot/css/app.css` - Update styles
3. `SharpTalk.Web/Shared/MobileNavigation.razor` - Fix duplicate menu
4. `SharpTalk.Web/Shared/ChannelList.razor` - Hide header on mobile
5. `SharpTalk.Web/Layout/MainLayout.razor.css` - Fix overlapping issues
6. `SharpTalk.Web/wwwroot/js/mobile.js` - Add tap-and-hold gesture (or create new file)

## Success Criteria

- [ ] User pill does not cover chat message input
- [ ] User pill shows only profile picture by default
- [ ] Tap-and-hold gesture expands the user pill
- [ ] User menu has smooth upward animation from pill shape
- [ ] No duplicate hamburger menus
- [ ] Workspace name is displayed cleanly without conflicts
- [ ] Settings button does not overlap with hamburger menu
- [ ] All changes work smoothly on mobile devices
