using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SpikeApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Root>("#app");
await builder.Build().RunAsync();
