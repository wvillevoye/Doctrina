// Saves a file the server streams to the browser (change add-bulk-import, design D7): import reports and templates.
// The standard Blazor pattern with a DotNetStreamReference, because /api/v1 needs a bearer token.

export async function save(fileName, streamReference) {
  const buffer = await streamReference.arrayBuffer();
  const url = URL.createObjectURL(new Blob([buffer], { type: "text/csv" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
