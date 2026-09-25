(() => {
    const getSessionRows = row =>
        Array.from(
            row.closest("table[data-session-grid]")?.querySelectorAll(
                "tbody tr[data-session-row]") ?? []);

    const focusRow = row => {
        row.focus({ preventScroll: true });
        row.scrollIntoView({ block: "nearest" });
    };

    document.addEventListener("pointerdown", event => {
        const row = event.target.closest?.("tr[data-session-row]");
        if (row) {
            focusRow(row);
        }
    });

    document.addEventListener("keydown", event => {
        const row = event.target.closest?.("tr[data-session-row]");
        if (row) {
            const rows = getSessionRows(row);
            const currentIndex = rows.indexOf(row);
            let targetIndex = currentIndex;

            switch (event.key) {
                case "ArrowUp":
                    targetIndex = Math.max(0, currentIndex - 1);
                    break;
                case "ArrowDown":
                    targetIndex = Math.min(rows.length - 1, currentIndex + 1);
                    break;
                case "Home":
                    targetIndex = 0;
                    break;
                case "End":
                    targetIndex = rows.length - 1;
                    break;
                case "ArrowRight": {
                    const detailPanel = document.querySelector(
                        "[data-session-detail]");
                    if (detailPanel) {
                        event.preventDefault();
                        detailPanel.focus({ preventScroll: true });
                        detailPanel.scrollIntoView({ block: "nearest" });
                    }
                    return;
                }
                case "Enter":
                case " ":
                    event.preventDefault();
                    row.click();
                    return;
                default:
                    return;
            }

            event.preventDefault();
            const targetRow = rows[targetIndex];
            if (targetRow) {
                focusRow(targetRow);
                if (targetRow !== row) {
                    targetRow.click();
                }
            }
            return;
        }

        const detailPanel = event.target.closest?.("[data-session-detail]");
        if (detailPanel
            && event.target === detailPanel
            && event.key === "ArrowLeft") {
            const selectedRow = document.querySelector(
                "table[data-session-grid] tbody tr[aria-selected='true']");
            if (selectedRow) {
                event.preventDefault();
                focusRow(selectedRow);
            }
        }
    });
})();
