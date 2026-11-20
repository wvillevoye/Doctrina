// Markdown editor toolbar (design D1 of add-markdown-toolbar): the only script of the Content module. The formatting
// rules live in C# (MarkdownFormatter); this module only reads and sets the text and selection of the textarea.

function field(container) {
  return container.querySelector("textarea");
}

/** Toolbar buttons must not take the focus on a mouse click, or the textarea loses its visible selection. */
export function keepSelection(container) {
  container.querySelector("[role=toolbar]").addEventListener("mousedown", (e) => {
    if (e.target.closest("button")) e.preventDefault();
  });
}

/** The current text and selection, as the user sees them (also text that did not fire a change event yet). */
export function getState(container) {
  const textarea = field(container);
  return { text: textarea.value, start: textarea.selectionStart, end: textarea.selectionEnd };
}

/** Sets text and selection, gives the focus back to the textarea and tells Blazor about the new value. */
export function setState(container, state) {
  const textarea = field(container);
  textarea.value = state.text;
  textarea.focus();
  textarea.setSelectionRange(state.start, state.end);
  textarea.dispatchEvent(new Event("input", { bubbles: true }));
  textarea.dispatchEvent(new Event("change", { bubbles: true }));
}

/** Makes a fixed window movable by its handle: pointer drag, and arrow keys while the handle has focus. */
export function makeDraggable(win, handle) {
  const place = (left, top) => {
    const r = win.getBoundingClientRect();
    win.style.right = "auto";
    win.style.bottom = "auto";
    win.style.left = Math.min(Math.max(0, left), innerWidth - r.width) + "px";
    win.style.top = Math.min(Math.max(0, top), innerHeight - r.height) + "px";
  };
  // Anchor at the top left from the start, so the native resize corner (bottom right) makes the window grow
  // to the right and down instead of away from the pointer.
  const start = win.getBoundingClientRect();
  place(start.left, start.top);
  handle.addEventListener("pointerdown", (e) => {
    const r = win.getBoundingClientRect();
    const dx = e.clientX - r.left;
    const dy = e.clientY - r.top;
    handle.setPointerCapture(e.pointerId);
    const move = (m) => place(m.clientX - dx, m.clientY - dy);
    const stop = () => {
      handle.removeEventListener("pointermove", move);
      handle.removeEventListener("pointerup", stop);
    };
    handle.addEventListener("pointermove", move);
    handle.addEventListener("pointerup", stop);
  });
  handle.addEventListener("keydown", (e) => {
    const step = { ArrowLeft: [-16, 0], ArrowRight: [16, 0], ArrowUp: [0, -16], ArrowDown: [0, 16] }[e.key];
    if (!step) return;
    e.preventDefault();
    const r = win.getBoundingClientRect();
    place(r.left + step[0], r.top + step[1]);
  });
}
