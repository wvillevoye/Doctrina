// Saves a file the server streams to the browser: import reports and templates (change add-bulk-import, design D7)
// and content files (change add-content, design D6). The standard Blazor pattern with a DotNetStreamReference,
// because /api/v1 needs a bearer token. The download attribute makes the browser save it, whatever its type.
// ponytail: the whole file passes through browser memory; a cookie-authenticated GET endpoint if files get large.

export async function save(fileName, streamReference) {
  const buffer = await streamReference.arrayBuffer();
  const url = URL.createObjectURL(new Blob([buffer], { type: "application/octet-stream" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
