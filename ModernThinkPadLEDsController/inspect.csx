using System;
using System.Reflection;
using System.Linq;

var asm = Assembly.LoadFrom(@"d:\Repos\System\modern-thinkpad-leds-controller\ModernThinkPadLEDsController\bin\Release\net10.0-windows\win-x64\LibreHardwareMonitorLib.dll");

Console.WriteLine("=== Looking for EC-related types ===\n");

Type[] types;
try {
    types = asm.GetTypes();
} catch (ReflectionTypeLoadException ex) {
    types = ex.Types.Where(t => t != null).ToArray();
    Console.WriteLine($"Loaded {types.Length} types (some failed)\n");
}

var ecTypes = types.Where(t => 
    t.FullName != null && (
        t.FullName.Contains("Embedded") || 
        t.FullName.Contains(".EC.") ||
        t.FullName.Contains("Ring") ||
        t.FullName.Contains("Port") ||
        t.Name.Contains("Port") ||
        t.Name.Contains("Ring")
    )
).ToList();

foreach (var type in ecTypes) {
    Console.WriteLine($"\nType: {type.FullName}");
    Console.WriteLine($"  Kind: {(type.IsInterface ? "Interface" : type.IsClass ? "Class" : "Other")}");
    
    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    foreach (var method in methods) {
        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
        Console.WriteLine($"    {method.ReturnType.Name} {method.Name}({parameters})");
    }
    
    var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    foreach (var prop in properties) {
        Console.WriteLine($"    Property: {prop.PropertyType.Name} {prop.Name}");
    }
}
