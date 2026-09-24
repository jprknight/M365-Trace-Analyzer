globalThis.m365Clipboard = {
    async writeText(text) {
        if (!navigator.clipboard?.writeText) {
            throw new Error("Clipboard access is not available in this browser.");
        }

        await navigator.clipboard.writeText(text);
    }
};
