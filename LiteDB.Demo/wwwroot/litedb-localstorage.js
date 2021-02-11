(function () {

    function flush(data) {

        const positions = Object.keys(data.pages);

        for (var position of positions) {

            const key = "lbl_pages_" + position.toString().padStart(5, "0");

            localStorage.setItem(key, data.pages[position]);
        }

        localStorage.setItem("lbl_length", data.length);
    }


    // export to window object
    window.localStorageStream = {
        flush
    }

})();