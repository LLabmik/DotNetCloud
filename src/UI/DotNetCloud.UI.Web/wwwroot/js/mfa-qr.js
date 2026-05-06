// MFA QR Code rendering — wraps qrcode.js for Blazor JS interop
window.dotnetcloudMfaQr = {
    render: function (canvasId, text, width, darkColor, lightColor) {
        const container = document.getElementById(canvasId);
        if (!container) {
            console.error('MFA QR: container element not found:', canvasId);
            return;
        }

        // Clear any previous content
        container.innerHTML = '';

        try {
            new QRCode(container, {
                text: text,
                width: width || 220,
                height: width || 220,
                colorDark: darkColor || '#000000',
                colorLight: lightColor || '#ffffff',
                correctLevel: QRCode.CorrectLevel.H
            });
        } catch (e) {
            console.error('MFA QR: failed to render QR code:', e);
        }
    },

    clear: function (canvasId) {
        const container = document.getElementById(canvasId);
        if (container) {
            container.innerHTML = '';
        }
    }
};
