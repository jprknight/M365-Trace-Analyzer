document.addEventListener("pointerdown", event => {
    const selectedMenu = event.target.closest(".filter-menu");

    document.querySelectorAll(".filter-menu[open]").forEach(menu => {
        if (menu !== selectedMenu) {
            menu.removeAttribute("open");
        }
    });
}, true);

document.addEventListener("toggle", event => {
    const selectedMenu = event.target;
    if (!selectedMenu.matches?.(".filter-menu[open]")) {
        return;
    }

    document.querySelectorAll(".filter-menu[open]").forEach(menu => {
        if (menu !== selectedMenu) {
            menu.removeAttribute("open");
        }
    });
}, true);
