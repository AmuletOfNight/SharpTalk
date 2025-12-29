# Chat Input Area Redesign Plan

## Overview
Redesign the chat input area in SharpTalk to create a sleek, modern interface that matches the Discord-inspired dark theme used throughout the application.

## Current Issues
1. **Attach Button**: Uses a plain emoji (📎) - lacks visual appeal and doesn't match the design system
2. **Send Button**: Plain text button with no icon or styling
3. **Input Container**: Basic layout without cohesive design
4. **File Preview**: Simple text-based preview without visual polish

## Design System Reference
- **Primary Color**: #5865f2 (Discord blurple)
- **Background Colors**: 
  - Main: #36393f
  - Input: #40444b
  - Sidebar: #202225
- **Text Colors**: #dcddde (primary), #b9bbbe (muted), #ffffff (headings)
- **Border Radius**: 4px, 8px, 12px, 16px (varies by component)
- **Transitions**: 0.2s ease for hover effects

## Proposed Design

### 1. Unified Input Container
Create a cohesive message input bar with:
- Background color: #40444b
- Rounded corners: 8px
- Subtle border: 1px solid #202225
- Flexbox layout for proper alignment
- Padding: 12px 16px

### 2. Attach Button (Left Side)
- **Icon**: SVG paperclip icon
- **Style**: Circular button (40px × 40px)
- **Background**: Transparent with hover effect
- **Hover**: Background color #5865f2, icon turns white
- **Transition**: 0.2s ease
- **Position**: Left side of input container

### 3. Message Input Field (Center)
- **Background**: Transparent
- **Border**: None
- **Color**: #dcddde
- **Placeholder**: #72767d
- **Flex**: 1 (takes available space)
- **Focus**: No outline, maintains clean look

### 4. Send Button (Right Side)
- **Icon**: SVG send/arrow icon
- **Style**: Circular button (40px × 40px)
- **Background**: #5865f2 (primary color)
- **Hover**: #4752c4 (darker shade)
- **Disabled**: #747f8d with reduced opacity
- **Transition**: 0.2s ease
- **Position**: Right side of input container

### 5. File Preview Area
- **Container**: Above input bar
- **Style**: Horizontal scrollable area
- **File Cards**:
  - Background: #2f3136
  - Border radius: 8px
  - Padding: 8px 12px
  - Display: flex, align-items: center
  - Gap: 8px
  - Max width: 200px
- **File Icon**: Color-coded based on file type
- **File Name**: Truncated with ellipsis
- **Remove Button**: Small × icon with hover effect

## Implementation Steps

### Step 1: Add CSS Styles
Add new CSS classes to `app.css`:
- `.chat-input-container` - Main container styling
- `.attach-btn` - Attach button styling
- `.send-btn` - Send button styling
- `.file-preview-area` - File preview container
- `.file-preview-card` - Individual file preview card
- `.file-preview-icon` - File type icon
- `.file-preview-remove` - Remove button styling

### Step 2: Update ChatArea.razor HTML
Restructure the message input section:
- Wrap input elements in unified container
- Replace emoji attach button with SVG icon
- Replace text send button with SVG icon
- Improve file preview structure

### Step 3: Add SVG Icons
Include inline SVG icons for:
- Paperclip (attach)
- Send arrow
- File type icons (image, pdf, document, archive, default)
- Close/remove icon

### Step 4: Responsive Design
Ensure the design works on:
- Desktop (full width)
- Tablet (adjusted padding)
- Mobile (stacked layout if needed)

## Visual Mockup

```
┌─────────────────────────────────────────────────────────┐
│ [File Preview Cards - Horizontal Scroll]                │
│ ┌──────────┐ ┌──────────┐ ┌──────────┐                 │
│ │ 📎 file1 │ │ 📄 file2 │ │ 🖼️ img1  │                 │
│ └──────────┘ └──────────┘ └──────────┘                 │
├─────────────────────────────────────────────────────────┤
│ ┌────┐ ┌────────────────────────────────────┐ ┌────┐   │
│ │ 📎 │ │ Type a message...                  │ │ ➤  │   │
│ └────┘ └────────────────────────────────────┘ └────┘   │
└─────────────────────────────────────────────────────────┘
```

## Color Scheme for File Type Icons
- **Images**: #faa61a (orange)
- **PDF**: #f04747 (red)
- **Documents**: #5865f2 (blurple)
- **Archives**: #3ba55c (green)
- **Default**: #747f8d (gray)

## Accessibility Considerations
- Maintain keyboard navigation
- Add aria-labels for icon-only buttons
- Ensure sufficient color contrast
- Support screen readers

## Browser Compatibility
- Modern browsers (Chrome, Firefox, Safari, Edge)
- SVG icons supported in all modern browsers
- CSS flexbox and transitions widely supported

## Success Criteria
- [ ] Attach button has sleek SVG icon with hover effect
- [ ] Send button has circular design with primary color
- [ ] Input container has unified, cohesive styling
- [ ] File preview area matches message attachment design
- [ ] All hover states and transitions work smoothly
- [ ] Design is responsive across screen sizes
- [ ] Matches existing design system colors and patterns
