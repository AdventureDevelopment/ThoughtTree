// Composition root of the client (architecture.md section 5): wires UI components and browser adapters
// (namespace ThoughtTree.Client.Adapters); no logic lives here. The root component is "Root", not "App",
// because "App" would clash with the namespace ThoughtTree.Client.App.
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ThoughtTree.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Root>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

await builder.Build().RunAsync().ConfigureAwait(false);
