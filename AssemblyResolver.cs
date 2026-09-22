using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace WDesk.Widgets.Music
{
    internal static class AssemblyResolver
    {
        private static bool _initialized = false;
        private static string _widgetFolder = null;

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                _widgetFolder = Path.GetDirectoryName(assembly.Location);

                if (string.IsNullOrEmpty(_widgetFolder))
                    return;

                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

                try
                {
                    AssemblyLoadContext.Default.Resolving += OnLoadContextResolving;
                }
                catch { }
            }
            catch { }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                if (string.IsNullOrEmpty(_widgetFolder)) return null;

                var assemblyName = new AssemblyName(args.Name);
                var dllName = assemblyName.Name + ".dll";
                var dllPath = Path.Combine(_widgetFolder, dllName);

                if (File.Exists(dllPath))
                    return Assembly.LoadFrom(dllPath);

                foreach (var subDir in Directory.GetDirectories(_widgetFolder))
                {
                    var subPath = Path.Combine(subDir, dllName);
                    if (File.Exists(subPath))
                        return Assembly.LoadFrom(subPath);
                }
            }
            catch { }

            return null;
        }

        private static Assembly OnLoadContextResolving(AssemblyLoadContext context, AssemblyName name)
        {
            try
            {
                if (string.IsNullOrEmpty(_widgetFolder)) return null;

                var dllName = name.Name + ".dll";
                var dllPath = Path.Combine(_widgetFolder, dllName);

                if (File.Exists(dllPath))
                    return context.LoadFromAssemblyPath(dllPath);
            }
            catch { }

            return null;
        }
    }
}