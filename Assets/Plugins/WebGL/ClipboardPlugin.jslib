mergeInto(LibraryManager.library, {
    CopyToClipboardJS: function(textPtr) {
        var text = UTF8ToString(textPtr);

        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(function() {
                console.log('Text copied to clipboard');
            }).catch(function(err) {
                console.error('Failed to copy text: ', err);
                // Fallback
                fallbackCopyTextToClipboard(text);
            });
        } else {
            fallbackCopyTextToClipboard(text);
        }

        function fallbackCopyTextToClipboard(text) {
            var textArea = document.createElement("textarea");
            textArea.value = text;
            textArea.style.top = "0";
            textArea.style.left = "0";
            textArea.style.position = "fixed";
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();
            try {
                document.execCommand('copy');
            } catch (err) {
                console.error('Fallback: Could not copy text', err);
            }
            document.body.removeChild(textArea);
        }
    }
});
