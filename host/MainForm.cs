using System.Text.Json;
using System.Linq;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ShellGems.Host
{
    public class MainForm : Form
    {
        private WebView2 webView;
        private readonly PluginManager _pluginManager = new PluginManager();

        public MainForm()
        {
            this.Text = "Shell Gems";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;

            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            this.Controls.Add(webView);

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webView.EnsureCoreWebView2Async(null);

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // 1. Check for the portable build structure (renderer/dist)
            var rendererDir = Path.Combine(baseDir, "renderer", "dist");
            
            if (!Directory.Exists(rendererDir))
            {
                // 2. Check for development structure (up several directories from output folder)
                rendererDir = Path.Combine(baseDir, "..", "..", "..", "..", "renderer", "dist", "renderer");
            }
            
            if (!Directory.Exists(rendererDir))
            {
                // 3. Last attempt — same level as host
                rendererDir = Path.Combine(baseDir, "..", "renderer", "dist");
            }

            if (!Directory.Exists(rendererDir))
            {
                throw new DirectoryNotFoundException($"Could not find the UI content directory at: {rendererDir}");
            }

            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.shellgems.local",
                rendererDir,
                CoreWebView2HostResourceAccessKind.Allow);

            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            webView.Source = new Uri("http://app.shellgems.local/index.html");
        }

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.WebMessageAsJson;
            if (string.IsNullOrEmpty(message)) return;

            string? channel = null;
            string? correlationId = null;

            try
            {
                var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                
                if (!root.TryGetProperty("channel", out var channelProp)) return;
                channel = channelProp.GetString()!;
                
                if (!root.TryGetProperty("correlationId", out var correlationProp)) return;
                correlationId = correlationProp.GetString();

                root.TryGetProperty("payload", out var payload);

                switch (channel)
                {
                    case "plugins:list":
                        var plugins = _pluginManager.ScanPlugins();
                        foreach (var p in plugins)
                        {
                            try { await _pluginManager.GetPluginFunctions(p.id); p.status = "active"; }
                            catch (Exception ex) { p.status = "error"; p.error = ex.Message; }
                        }
                        SendResponse(channel, correlationId, new { success = true, plugins });
                        break;

                    case "plugins:functions":
                        if (payload.ValueKind != JsonValueKind.Null && payload.ValueKind != JsonValueKind.Undefined)
                        {
                            var pluginId = payload.GetProperty("pluginId").GetString()!;
                            var functions = await _pluginManager.GetPluginFunctions(pluginId);
                            SendResponse(channel, correlationId, new { success = true, pluginId, functions });
                        }
                        break;

                    case "plugins:params":
                        if (payload.ValueKind != JsonValueKind.Null && payload.ValueKind != JsonValueKind.Undefined)
                        {
                            var pId = payload.GetProperty("pluginId").GetString()!;
                            var fName = payload.GetProperty("functionName").GetString()!;
                            var prms = await _pluginManager.GetPluginParams(pId, fName);
                            SendResponse(channel, correlationId, new { success = true, pluginId = pId, functionName = fName, @params = prms });
                        }
                        break;

                    case "plugins:execute":
                        if (payload.ValueKind != JsonValueKind.Null && payload.ValueKind != JsonValueKind.Undefined)
                        {
                            var exPId = payload.GetProperty("pluginId").GetString()!;
                            var exFName = payload.GetProperty("functionName").GetString()!;
                            var exParams = payload.GetProperty("params");
                            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(exParams.GetRawText());
                            
                            // Convert JsonElements to primitive types
                            var primitiveDict = dict?.ToDictionary(
                                k => k.Key,
                                v => ConvertJsonElement(v.Value)
                            );

                            var result = await _pluginManager.ExecutePlugin(exPId, exFName, primitiveDict!);
                            SendResponse(channel, correlationId, new { success = true, pluginId = exPId, functionName = exFName, result });
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (channel != null && correlationId != null)
                {
                    SendResponse(channel, correlationId, new { success = false, error = ex.Message });
                }
            }
        }

        private object? ConvertJsonElement(object? value)
        {
            if (value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String: return element.GetString();
                    case JsonValueKind.Number: return element.GetDouble(); // Most plugins expect double or int
                    case JsonValueKind.True: return true;
                    case JsonValueKind.False: return false;
                    case JsonValueKind.Null: return null;
                    default: return element.GetRawText();
                }
            }
            return value;
        }

        private void SendResponse(string channel, string? correlationId, object result)
        {
            var response = new
            {
                channel = $"{channel}:result:{correlationId}",
                payload = result
            };
            webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(response));
        }
    }
}
