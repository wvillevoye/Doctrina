// DcModal (design D10): the only script code of Doctrina.UI. On showModal() the browser makes the rest of the page inert,
// handles focus and Escape; this script only remembers who had the focus and restores it.

export function show(dialog, dotNet) {
  if (dialog.open) {
    return;
  }

  dialog.dcReturnFocus = document.activeElement;
  dialog.dcDotNet = dotNet;
  if (!dialog.dcListening) {
    dialog.addEventListener("close", () => {
      const target = dialog.dcReturnFocus;
      dialog.dcReturnFocus = null;
      if (target && typeof target.focus === "function" && document.contains(target)) {
        target.focus();
      }
      dialog.dcDotNet?.invokeMethodAsync("OnClosed");
    });
    dialog.dcListening = true;
  }

  dialog.showModal();
}

export function close(dialog) {
  if (dialog.open) {
    dialog.close();
  }
}
