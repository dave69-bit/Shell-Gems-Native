using System.Reflection;
using System.Text.Json;
using System.Dynamic;

namespace ShellGems.Host
{
    public class PluginInfo
    {
        public string id { get; set; } = "";
        public string name { get; set; } = "";
        public string iconPath { get; set; } = "";
        public string status { get; set; } = "pending";
        public string? error { get; set; }
    }

    public class PluginManager
    {
        private string GetPluginsDir()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var pluginsDir = Path.Combine(baseDir, "plugins");
            
            if (!Directory.Exists(pluginsDir))
            {
                // Fallback for development (assuming host is in a folder within the project)
                pluginsDir = Path.Combine(baseDir, "..", "..", "..", "..", "plugins");
            }
            
            return pluginsDir;
        }

        public List<PluginInfo> ScanPlugins()
        {
            var pluginsDir = GetPluginsDir();
            var dllsDir = Path.Combine(pluginsDir, "dlls");
            var iconsDir = Path.Combine(pluginsDir, "icons");

            var plugins = new List<PluginInfo>();

            if (!Directory.Exists(dllsDir)) return plugins;

            foreach (var file in Directory.GetFiles(dllsDir, "*.dll"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                var name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(id.Replace("-", " "));
                var iconPathRelative = $"plugins/icons/{id}.png";
                var iconPathAbsolute = Path.Combine(iconsDir, $"{id}.png");

                plugins.Add(new PluginInfo
                {
                    id = id,
                    name = name,
                    iconPath = File.Exists(iconPathAbsolute) ? iconPathRelative : "",
                    status = "pending"
                });
            }

            return plugins;
        }

        public async Task<object> GetPluginFunctions(string pluginId)
        {
            var plugin = LoadPluginInstance(pluginId);
            var method = plugin.GetType().GetMethod("GetFunctions");
            if (method == null) throw new Exception("GetFunctions method not found");
            
            var task = (Task<object>)method.Invoke(plugin, new object[] { null! })!;
            return await task;
        }

        public async Task<object> GetPluginParams(string pluginId, string functionName)
        {
            var plugin = LoadPluginInstance(pluginId);
            var method = plugin.GetType().GetMethod("GetParams");
            if (method == null) throw new Exception("GetParams method not found");
            
            dynamic input = new ExpandoObject();
            input.functionName = functionName;
            
            var task = (Task<object>)method.Invoke(plugin, new object[] { (object)input })!;
            return await task;
        }

        public async Task<object> ExecutePlugin(string pluginId, string functionName, object parameters)
        {
            var plugin = LoadPluginInstance(pluginId);
            var method = plugin.GetType().GetMethod("Execute");
            if (method == null) throw new Exception("Execute method not found");
            
            dynamic input = new ExpandoObject();
            input.functionName = functionName;
            input.parameters = parameters;
            
            var task = (Task<object>)method.Invoke(plugin, new object[] { (object)input })!;
            return await task;
        }

        private object LoadPluginInstance(string pluginId)
        {
            var dllPath = Path.Combine(GetPluginsDir(), "dlls", $"{pluginId}.dll");
            if (!File.Exists(dllPath)) throw new FileNotFoundException($"DLL not found: {dllPath}");

            var assembly = Assembly.LoadFrom(dllPath);
            var typeName = $"Shell_Gems.Plugins.{pluginId.Replace("-", "")}Plugin";
            var type = assembly.GetTypes().FirstOrDefault(t => t.FullName?.EndsWith(typeName, StringComparison.OrdinalIgnoreCase) == true);
            
            if (type == null) throw new Exception($"Type {typeName} not found in {dllPath}. Available types: {string.Join(", ", assembly.GetTypes().Select(t => t.FullName))}");

            return Activator.CreateInstance(type)!;
        }
    }
}
