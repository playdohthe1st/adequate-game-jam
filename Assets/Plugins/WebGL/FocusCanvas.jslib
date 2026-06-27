mergeInto(LibraryManager.library, {
    FocusCanvas: function () {
        if (document.activeElement && document.activeElement !== document.body)
            document.activeElement.blur();
    }
});
