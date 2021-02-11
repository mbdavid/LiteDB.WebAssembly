(function () {



    function getLength() {
        return localStorage.getItem("litedb_length") || 0;
    }

    function setLength(value) {
        return localStorage.setItem("litedb_length", value);
    }

    function getPage(position) {
        const page = (position / PAGE_SIZE).toString();
        return (PAGE_PAD.substring(0, PAGE_PAD.length - page.length)) + page;
    }

    // read 8kb page from file position
    function readBytes(position) {
        return localStorage.getItem("litedb_page_" + getPage(position))
    }

    // write 8kb content inside page position
    function writeBytes(position, content) {
        localStorage.setItem("litedb_page_" + getPage(position), content);
        if (position + PAGE_SIZE > getLength()) {
            setLength(position + PAGE_SIZE);
        }
    }


    // export to window object
    window.localStorageDb = {
        getLength,
        readBytes,
        writeBytes
    }

})();