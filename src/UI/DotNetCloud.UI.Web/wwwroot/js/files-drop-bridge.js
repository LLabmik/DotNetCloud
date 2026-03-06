window.dotnetcloudFilesDrop = window.dotnetcloudFilesDrop || {
    init: function(dropZoneSelector, inputSelector) {
        const dropZone = document.querySelector(dropZoneSelector);
        const input = document.querySelector(inputSelector);

        if (!dropZone || !input) {
            return false;
        }

        if (dropZone.dataset.dncDropBridgeInit === "1") {
            return true;
        }

        dropZone.dataset.dncDropBridgeInit = "1";

        dropZone.addEventListener("drop", function(event) {
            event.preventDefault();
            event.stopPropagation();

            const dt = event.dataTransfer;
            if (!dt || !dt.files || dt.files.length === 0) {
                return;
            }

            try {
                const transfer = new DataTransfer();
                for (const file of dt.files) {
                    transfer.items.add(file);
                }

                input.files = transfer.files;
                input.dispatchEvent(new Event("change", { bubbles: true }));
            } catch (err) {
                console.error("DotNetCloud drop bridge failed:", err);
            }
        }, true);

        return true;
    }
};
