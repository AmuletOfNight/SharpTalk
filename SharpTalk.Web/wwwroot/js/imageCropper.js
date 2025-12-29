window.imageCropper = {
    canvas: null,
    image: null,
    selection: null,

    init: function (canvasId, imageSrc) {
        // Robust definition of web components for v2
        const defineIfExist = (name, obj) => {
            if (obj && typeof obj.$define === 'function') {
                obj.$define();
                console.log(`Defined ${name}`);
            }
        };

        if (typeof Cropper !== 'undefined') {
            defineIfExist('Canvas', Cropper.Canvas);
            defineIfExist('Image', Cropper.Image);
            defineIfExist('Selection', Cropper.Selection);
            defineIfExist('Handle', Cropper.Handle);
        } else {
            // Fallback for global defines
            defineIfExist('CropperCanvas', window.CropperCanvas);
            defineIfExist('CropperImage', window.CropperImage);
            defineIfExist('CropperSelection', window.CropperSelection);
            defineIfExist('CropperHandle', window.CropperHandle);
        }

        this.canvas = document.getElementById(canvasId);
        if (!this.canvas) return;

        this.image = this.canvas.querySelector('cropper-image');
        this.selection = this.canvas.querySelector('cropper-selection');

        if (this.image) {
            this.image.src = imageSrc;
        }
    },

    getCroppedImage: async function () {
        if (!this.selection) return null;

        try {
            // v2 $toCanvas is asynchonous
            const canvas = await this.selection.$toCanvas();
            if (!canvas) return null;

            return canvas.toDataURL('image/webp', 0.9);
        } catch (e) {
            console.error('Error in getCroppedImage:', e);
            return null;
        }
    },

    destroy: function () {
        this.canvas = null;
        this.image = null;
        this.selection = null;
    }
};
