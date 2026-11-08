using System.Globalization;
using Dagligvarer.Web.Components;
using Dagligvarer.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// Danske tal og datoer overalt (komma som decimaltegn).
var danish = new CultureInfo("da-DK");
CultureInfo.DefaultThreadCurrentCulture = danish;
CultureInfo.DefaultThreadCurrentUICulture = danish;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<PriceQueries>();

await builder.Build().RunAsync();
