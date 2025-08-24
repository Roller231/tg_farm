mergeInto(LibraryManager.library, {
    CopyToClipboard: function (text) {
        const str = Pointer_stringify(text);
        
        // 1. Пробуем Clipboard API (современный способ)
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(str)
                .then(() => {
                    console.log("Скопировано через Clipboard API");
                })
                .catch(() => {
                    // Если Clipboard API не сработал, используем fallback
                    fallbackCopy(str);
                });
        } else {
            // 2. Если Clipboard API не поддерживается, сразу идём к fallback
            fallbackCopy(str);
        }

        // Классический метод копирования
        function fallbackCopy(text) {
            const textArea = document.createElement("textarea");
            textArea.value = text;
            textArea.style.position = "fixed";
            textArea.style.opacity = "0";
            
            document.body.appendChild(textArea);
            textArea.select();
            
            try {
                document.execCommand("copy");
                console.log("Скопировано через fallback метод");
            } catch (err) {
                console.error("Ошибка копирования:", err);
                // Показываем текст для ручного копирования
                prompt("Скопируйте текст вручную:", text);
            } finally {
                document.body.removeChild(textArea);
            }
        }
    }
});