open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.Extensions.DependencyInjection
open Models
open MongoDB.Driver
open System.Text.Json.Serialization
open System

// NOTE: Initialize Mongo

let initializeMongo () =
  let connectionString = Environment.GetEnvironmentVariable("MONGODB_URI")
  match connectionString with
  | null ->
    printfn "You must set your 'MONGODB_URI' environmental variable. See\n\t https://www.mongodb.com/docs/drivers/go/current/usage-examples/#environment-variable"
    exit 1
  | _ ->
    let client = new MongoClient(connectionString)
    let collection = client.GetDatabase("admin").GetCollection<Course>("courses")

    // NOTE: Used to create the initial record. Running again causes an error.
    // collection.InsertOne (
    //   { Id = "intro" 
    //     Content = "hello world"
    //     Metadata =
    //       { Description = "Some description"
    //         Name = "Introduction to Edu" } },
    //   null,
    //   new CancellationToken()
    // )

    let filter = Builders<Course>.Filter.Eq("Id", "intro")
    let document = collection.Find(filter).First()
    
    printfn "Proof mongo works...you should see a record following:\n%s" (document.ToString())

initializeMongo()

let webApp = (choose
  [
    GET >=>
      routex "(/?)" >=> Index.get
    subRoute "/api" (choose
      [
        GET >=>
          routex "(/?)" >=> Api.Index.get
        subRoute "/v1" (choose
          [
            GET >=> (choose
              [
                routex "(/?)" >=> Api.V1.Index.get
                routex  "/blog(/?)" >=> Api.V1.Blog.getAll
                routef  "/blog/%s" Api.V1.Blog.get
                routex  "/course(/?)" >=> Api.V1.Course.getAll
                routef  "/course/%s" Api.V1.Course.get
              ]
            )
          ]
        )
      ]
    )
  ]
)

let configureCors (builder : CorsPolicyBuilder) =
  builder
    .WithOrigins(
      // NOTE: Development client
      "http://localhost:5173",
      // NOTE: Development server
      "http://localhost:5000",
      // NOTE: Production client
      "https://edu.exokomodo.com",
      // NOTE: Production server
      "https://services.edu.exokomodo.com"
    )
    .AllowAnyMethod()
    .AllowAnyHeader() |> ignore

let configureServices (services : IServiceCollection) =
  services
    .AddCors()
    .AddGiraffe()
  |> ignore

  let serializationOptions = SystemTextJson.Serializer.DefaultOptions
  serializationOptions.Converters.Add(JsonFSharpConverter(JsonUnionEncoding.FSharpLuLike))
  services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(serializationOptions)) |> ignore

let builder = WebApplication.CreateBuilder()

configureServices builder.Services

let app = builder.Build()
// NOTE: Order matters. CORS must be configured before starting Giraffe.
app.UseCors configureCors |> ignore
app.UseGiraffe webApp
app.Run()

type Program() = class end
