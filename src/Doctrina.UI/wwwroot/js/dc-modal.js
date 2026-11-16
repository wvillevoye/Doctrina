// DcModal (design D10): de enige scriptcode van Doctrina.UI. De browser regelt bij showModal() het onbereikbaar maken
// van de rest van de pagina, de focus en Escape; dit script onthoudt alleen wie de focus had en zet die terug.

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
