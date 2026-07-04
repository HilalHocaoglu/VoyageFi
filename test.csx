var assm = System.Reflection.Assembly.LoadFrom("/Users/hilalhocaoglu/.nuget/packages/microsoft.openapi/2.7.5/lib/net8.0/Microsoft.OpenApi.dll");
foreach(var type in assm.GetTypes().Where(t => t.Name.Contains("OpenApiSecurityScheme") || t.Name.Contains("OpenApiInfo"))) {
    Console.WriteLine(type.FullName);
}
