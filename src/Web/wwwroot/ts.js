// Trigger a browser download from base64 bytes (e.g. a generated .xlsx).
window.tsDownload = (filename, base64, mime) => {
  const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
  const blob = new Blob([bytes], { type: mime || "application/octet-stream" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
};

// Copy-to-clipboard for [data-copy] buttons. This runs as a native capture-
// phase listener so the clipboard write happens synchronously INSIDE the
// browser's user gesture — routing it through Blazor JS interop runs after
// the gesture expires and clipboard APIs reject the write (silently).
document.addEventListener("click", (e) => {
  const el = e.target.closest?.("[data-copy]");
  if (el) window.tsCopy(el.getAttribute("data-copy"));
}, true);

// Clipboard write with non-secure-context fallback.
window.tsCopy = (text) => {
  if (navigator.clipboard && navigator.clipboard.writeText) {
    return navigator.clipboard.writeText(String(text)).then(() => true).catch(() => false);
  }
  // Fallback for non-secure contexts.
  try {
    const ta = document.createElement("textarea");
    ta.value = String(text);
    ta.style.position = "fixed";
    ta.style.opacity = "0";
    document.body.appendChild(ta);
    ta.select();
    document.execCommand("copy");
    document.body.removeChild(ta);
    return true;
  } catch {
    return false;
  }
};
