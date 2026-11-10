using Catalog.API.Data;
using Catalog.API.Integration;
using Catalog.API.Integration.ItemEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Add service for managing a sqlserver database that will be managed using CatalogContext
// the connection to the database is defined in appsettings

// Elegir la conexión según la variable de entorno DB2USE.
// En caso de que el entorno de despliegue sea producción (Docker) se usa SQL Server en contenedor
string? db2Use = Environment.GetEnvironmentVariable("DB2USE");

if (db2Use == "DockerSQLServer")
{
    builder.Services.AddDbContext<CatalogContext>(options =>
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("SQLServerDockerConnection"),
            sqlOptions =>
            {
                // Recuperamos el nombre del ensamblado donde está la migración
                sqlOptions.MigrationsAssembly(
                    Assembly.GetExecutingAssembly().GetName().Name);

                // Configuramos una conexión resiliente a la BD
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });
    });
}
else
{
    builder.Services.AddDbContext<CatalogContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection")));
}

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(
     options => {
         options.SwaggerDoc("v1",
            new OpenApiInfo
            {  // Uso el array de parámetros por nombre.
                Title = "API Rest del Ejemplo eShopOnContainers (catálogo de vinilos)",
                Contact = new OpenApiContact
                {
                    Email = "marcoabellantamara@gmail.com",
                    Name = "Marco Abellán Tamara",
                    Url = new Uri("https://github.com/mat31ua")
                },
                License = new OpenApiLicense
                {
                    Name = "Apache License, 2.0",
                    Url = new Uri("https://www.apache.org/licenses/LICENSE-2.0.html")
                },
                // Cuidado que Version es obligatorio.
                Version = "v1"
            });

         // Para añadir la información de documentación del XML generado a Swagger.
         var xmlfile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
         var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlfile);

     });

// Opciones de conexión a RabbitMQ y publicación de eventos de Catalog.API
builder.Services.Configure<RabbitOptions>(builder.Configuration.GetSection("RabbitMq"));

builder.Services.Configure<RabbitMqExchange>(builder.Configuration.GetSection("RabbitMqExchange"));

builder.Services.AddSingleton<IEventsPublisher, EventsPublisher>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    try
    {

        var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();

        //it creates the DB in case it does not exist
        //this is used only while developing the system

        db.Database.Migrate();

    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

app.MapControllers();

try
{

    //registering the publisher
    app.Lifetime.ApplicationStarted.Register(async () => {
        var eventsPublisher = app.Services.GetRequiredService<IEventsPublisher>();
        try
        {
            await eventsPublisher.Start();
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error while connecting to RabbitMq.");
        }
    });
    app.Lifetime.ApplicationStopping.Register(() => {
        var eventsPublisher = app.Services.GetRequiredService<IEventsPublisher>();
        eventsPublisher.Stop().GetAwaiter().GetResult();
    });

}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error while connecting to RabbitMq.");

}

app.Run();
