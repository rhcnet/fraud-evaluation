using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;

namespace FraudEvaluation.API.Extensions
{
    public static class DevelopmentExtensions
    {
        public static void UseDevelopmentConfiguration(this WebApplication app)
        {
            if (!app.Environment.IsDevelopment())
                return;

            // Expose OpenAPI and Swagger UI in Development
            app.MapOpenApi();

            // Scalar.AspNetCore integration (development only, no authentication)
            try
            {
                var scalarAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "Scalar.AspNetCore", StringComparison.OrdinalIgnoreCase)) ?? Assembly.Load("Scalar.AspNetCore");
                if (scalarAssembly != null)
                {
                    var extensionMethods = scalarAssembly.GetTypes()
                        .Where(t => t.IsSealed && t.IsAbstract)
                        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        .Where(m => m.Name.Contains("UseScalar", StringComparison.OrdinalIgnoreCase)
                                 || m.Name.Contains("MapScalar", StringComparison.OrdinalIgnoreCase)
                                 || m.Name.Contains("UseScalarUI", StringComparison.OrdinalIgnoreCase)
                                 || m.Name.Contains("MapScalarUI", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var method in extensionMethods)
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(app.GetType()))
                        {
                            method.Invoke(null, new object[] { app });
                            break;
                        }

                        if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(Microsoft.AspNetCore.Builder.IApplicationBuilder)))
                        {
                            method.Invoke(null, new object[] { app });
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Scalar integration is optional in development; ignore failures
            }
        }
    }
}
