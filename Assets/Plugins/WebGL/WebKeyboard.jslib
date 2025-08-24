mergeInto(LibraryManager.library, {
  OpenKeyboardJS: function (gameObjectPtr, methodNamePtr, placeholderPtr) {
    var gameObject = UTF8ToString(gameObjectPtr);
    var methodName = UTF8ToString(methodNamePtr);
    var placeholder = UTF8ToString(placeholderPtr);

    var input = document.createElement("input");
    input.type = "text";
    input.placeholder = placeholder || "";
    input.autocapitalize = "none";
    input.autocorrect = "off";
    input.spellcheck = false;

    // Ключевая магия:
    input.style.position = "fixed";
    input.style.bottom = "0";
    input.style.left = "50%";
    input.style.transform = "translateX(-50%)";
    input.style.width = "1px";
    input.style.height = "1px";
    input.style.opacity = "0.01";
    input.style.zIndex = "2147483647"; // максимум
    input.style.border = "none";
    input.style.background = "transparent";
    input.style.outline = "none";
    input.style.color = "transparent";

    document.body.appendChild(input);
    input.focus();

    input.addEventListener("blur", function () {
      if (typeof unityInstance !== 'undefined') {
        unityInstance.SendMessage(gameObject, methodName, input.value);
      }
      document.body.removeChild(input);
    });

    input.addEventListener("keydown", function (e) {
      if (e.key === "Enter") input.blur();
    });
  }
});
