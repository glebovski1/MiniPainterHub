using Microsoft.JSInterop;
using System.Net.Http.Headers;

namespace MiniPainterHub.WebApp
{
    public class JwtAuthorizationMessageHandler : DelegatingHandler
    {
        private readonly IJSRuntime _js;
        public JwtAuthorizationMessageHandler(IJSRuntime js) => _js = js;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var token = await _js.InvokeAsync<string>("localStorage.getItem", "authToken");
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await base.SendAsync(request, ct);
        }
    }
}
